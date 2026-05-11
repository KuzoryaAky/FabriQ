using System.Drawing;
using System.Linq.Expressions;
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
        double threshold = 100;
        if (request.Image == null || request.Image.Length == 0)
            throw new ArgumentException("Изображение не может быть пустым");

        Mat result = null;
        Mat original = null;
        Mat gray = null;
        Mat blurred = null;
        Mat edges = null;
        VectorOfVectorOfPoint contours = null;
        MemoryStream ms = null;

        try
        {
            byte[] imageBytes;
            using (var memoryStream = new MemoryStream())
            {
                await request.Image.CopyToAsync(memoryStream);
                imageBytes = memoryStream.ToArray();
            }

            // Базовая папка для сохранения
            string baseFolder = "D:\\Projects\\source\\repos\\FabriQ\\ImageProcessor\\Uploads";
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            // Создаем подпапку для этапов обработки
            string processingFolder = Path.Combine(baseFolder, $"processing_{timestamp}");
            Directory.CreateDirectory(processingFolder);

            // ШАГ 1: Загружаем изображение из байт
            original = new Mat();
            CvInvoke.Imdecode(imageBytes, ImreadModes.AnyColor, original);

            if (original.IsEmpty)
                throw new Exception("Не удалось загрузить изображение");

            // Сохраняем оригинал
            SaveMatToFile(original, Path.Combine(processingFolder, "01_original.png"));

            // ШАГ 2: Конвертируем в оттенки серого
            gray = new Mat();
            CvInvoke.CvtColor(original, gray, ColorConversion.Bgr2Gray);
            SaveMatToFile(gray, Path.Combine(processingFolder, "02_grayscale.png"));

            // ШАГ 3: Размытие для уменьшения шума
            blurred = new Mat();
            CvInvoke.GaussianBlur(gray, blurred, new System.Drawing.Size(5, 5), 1.5);
            SaveMatToFile(blurred, Path.Combine(processingFolder, "03_blurred.png"));

            // ШАГ 4: Поиск границ Canny
            edges = new Mat();
            CvInvoke.Canny(blurred, edges, threshold, threshold * 2);
            SaveMatToFile(edges, Path.Combine(processingFolder, "04_edges.png"));

            // ШАГ 5: Поиск контуров
            contours = new VectorOfVectorOfPoint();
            CvInvoke.FindContours(
                edges,
                contours,
                null,
                RetrType.External,
                ChainApproxMethod.ChainApproxSimple
            );

            // ШАГ 6: Создаем изображения для визуализации контуров

            // Вариант А: Контуры на черном фоне
            Mat contoursOnly = new Mat(edges.Size, DepthType.Cv8U, 3);
            contoursOnly.SetTo(new MCvScalar(0, 0, 0)); // Черный фон

            for (int i = 0; i < contours.Size; i++)
            {
                CvInvoke.DrawContours(
                    contoursOnly,
                    contours,
                    i,
                    new MCvScalar(0, 255, 0), // Зеленые контуры
                    1 // Толщина линии
                );
            }
            SaveMatToFile(contoursOnly, Path.Combine(processingFolder, "05_contours_black_bg.png"));
            contoursOnly.Dispose();

            // Вариант Б: Контуры на оригинальном изображении (полутоновое)
            Mat contoursOnGray = gray.Clone();
            CvInvoke.CvtColor(contoursOnGray, contoursOnGray, ColorConversion.Gray2Bgr);

            for (int i = 0; i < contours.Size; i++)
            {
                CvInvoke.DrawContours(
                    contoursOnGray,
                    contours,
                    i,
                    new MCvScalar(0, 255, 0), // Зеленые контуры
                    2
                );
            }
            SaveMatToFile(contoursOnGray, Path.Combine(processingFolder, "06_contours_on_gray.png"));
            contoursOnGray.Dispose();

            // Вариант В: Контуры на оригинальном цветном изображении
            result = original.Clone();

            for (int i = 0; i < contours.Size; i++)
            {
                CvInvoke.DrawContours(
                    result,
                    contours,
                    i,
                    new MCvScalar(0, 255, 0), // BGR: зеленый
                    2
                );
            }

            // Сохраняем финальный результат
            SaveMatToFile(result, Path.Combine(processingFolder, "07_final_result.png"));

            // Сохраняем также финальный результат в основную папку
            string finalPath = Path.Combine(baseFolder, $"image_{timestamp}.png");
            SaveMatToFile(result, finalPath);

            Console.WriteLine($"Все этапы обработки сохранены в папку: {processingFolder}");

            return default;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return null;
        }
        finally
        {
            // Важно! Освобождаем все ресурсы
            original?.Dispose();
            gray?.Dispose();
            blurred?.Dispose();
            edges?.Dispose();
            contours?.Dispose();
            result?.Dispose();
            ms?.Dispose();
        }
    }

    //// Вспомогательный метод для сохранения Mat в файл
    private void SaveMatToFile(Mat mat, string filePath)
    {
        try
        {
            VectorOfByte buffer = new VectorOfByte();
            CvInvoke.Imencode(".png", mat, buffer);
            File.WriteAllBytes(filePath, buffer.ToArray());
            buffer.Dispose();
            Console.WriteLine($"Сохранено: {Path.GetFileName(filePath)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при сохранении {filePath}: {ex.Message}");
        }
    }


    //public async Task<MeasurementResponse> ProcessImageAsync(MeasurementRequest request)
    //{
    //    return default;
    //}
}
