using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using ImageProcessor.Models.Requests;
using ImageProcessor.Models.Responses;
using ImageProcessor.Services.Interfaces;

public class ImageProcessingService : IImageProcessingService
{
    private readonly ILogger<ImageProcessingService> _logger;

    public ImageProcessingService(ILogger<ImageProcessingService> logger)
    {
        _logger = logger;
    }

    public async Task<MeasurementResponse> ProcessImageAsync(MeasurementRequest request)
    {
        if (request.Image == null || request.Image.Length == 0)
            throw new ArgumentException("Изображение не может быть пустым");

        Mat original = null;
        Mat result = null;

        try
        {
            byte[] imageBytes;
            using (var memoryStream = new MemoryStream())
            {
                await request.Image.CopyToAsync(memoryStream);
                imageBytes = memoryStream.ToArray();
            }

            string baseFolder = "D:\\Projects\\source\\repos\\FabriQ\\ImageProcessor\\Uploads";
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string processingFolder = Path.Combine(baseFolder, $"processing_{timestamp}");
            Directory.CreateDirectory(processingFolder);

            // Загрузка
            original = new Mat();
            CvInvoke.Imdecode(imageBytes, ImreadModes.AnyColor, original);
            if (original.IsEmpty)
                throw new Exception("Не удалось загрузить изображение");
            SaveMatToFile(original, Path.Combine(processingFolder, "01_original.png"));

            // ========== HSV ОБРАБОТКА ==========
            // ШАГ 1: Переводим в HSV
            Mat hsv = new Mat();
            CvInvoke.CvtColor(original, hsv, ColorConversion.Bgr2Hsv);

            // ШАГ 2: Разделяем каналы
            Mat[] channels = hsv.Split();
            Mat hue = channels[0];
            Mat saturation = channels[1];
            Mat value = channels[2];

            // Сохраняем каналы для отладки
            SaveMatToFile(hue, Path.Combine(processingFolder, "02_hue.png"));
            SaveMatToFile(saturation, Path.Combine(processingFolder, "02_saturation.png"));
            SaveMatToFile(value, Path.Combine(processingFolder, "02_value.png"));

            // ШАГ 3: Создаём маску (выберите один вариант)

            // ВАРИАНТ 1: По насыщенности (камень менее насыщен, чем фон)
            Mat mask = new Mat();
            CvInvoke.Threshold(saturation, mask, 31, 256, ThresholdType.Binary);

            // ВАРИАНТ 2: По оттенку (раскомментируйте и настройте диапазон)
            // Mat mask = new Mat();
            // CvInvoke.InRange(hue, new ScalarArray(10), new ScalarArray(30), mask);

            // ВАРИАНТ 3: По яркости (раскомментируйте при необходимости)
            // Mat mask = new Mat();
            // CvInvoke.Threshold(value, mask, 100, 255, ThresholdType.BinaryInv);

            SaveMatToFile(mask, Path.Combine(processingFolder, "03_mask.png"));

            // ШАГ 5: Поиск контуров на маске
            VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();
            Mat hierarchy = new Mat();
            CvInvoke.FindContours(mask, contours, hierarchy, RetrType.External, ChainApproxMethod.ChainApproxSimple);



            // Сохраняем все контуры для отладки
            Mat allContoursImg = original.Clone();
            for (int i = 0; i < contours.Size; i++)
            {
                CvInvoke.DrawContours(allContoursImg, contours, i, new MCvScalar(0, 255, 0), 1);
            }
            SaveMatToFile(allContoursImg, Path.Combine(processingFolder, "05_all_contours.png"));
            allContoursImg.Dispose();

            // ШАГ 6: Находим контур с максимальной площадью
            int targetIdx = -1;
            double maxArea = 0;
            Rectangle bestBox = new Rectangle();

            for (int i = 0; i < contours.Size; i++)
            {
                double area = CvInvoke.ContourArea(contours[i]);
                Rectangle rect = CvInvoke.BoundingRectangle(contours[i]);

                // Проверка: не касается краёв
                bool touchesEdge = rect.X <= 1 || rect.Y <= 1 ||
                                   rect.X + rect.Width >= mask.Width - 1 ||
                                   rect.Y + rect.Height >= mask.Height - 1;

                if (touchesEdge)
                    continue;

                // Берём самый большой по площади
                if (area > maxArea)
                {
                    maxArea = area;
                    targetIdx = i;
                    bestBox = rect;
                }
            }

            // Отрисовка результата
            result = original.Clone();

            if (targetIdx != -1)
            {
                VectorOfPoint hull = new VectorOfPoint();
                CvInvoke.ConvexHull(contours[targetIdx], hull);
                CvInvoke.DrawContours(result, new VectorOfVectorOfPoint(hull), 0, new MCvScalar(0, 255, 0), 3);
            }

            SaveMatToFile(result, Path.Combine(processingFolder, "06_result.png"));

            // Очистка HSV каналов
            hue.Dispose();
            saturation.Dispose();
            value.Dispose();
            hsv.Dispose();
            mask.Dispose();
            hierarchy.Dispose();

            return new MeasurementResponse();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обработки");
            return null;
        }
        finally
        {
            original?.Dispose();
            result?.Dispose();
        }
    }

    private void SaveMatToFile(Mat mat, string filePath)
    {
        try
        {
            if (mat == null || mat.IsEmpty)
            {
                Console.WriteLine($"Ошибка: mat пустой для {filePath}");
                return;
            }
            VectorOfByte buffer = new VectorOfByte();
            CvInvoke.Imencode(".png", mat, buffer);
            File.WriteAllBytes(filePath, buffer.ToArray());
            buffer.Dispose();
            Console.WriteLine($"СОХРАНЕНО: {filePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ОШИБКА сохранения {filePath}: {ex.Message}");
        }
    }
}