using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ImageProcessor.Services
{
    public class OpenCvExperiment
    {
        public class DetectionResult
        {
            public bool Success { get; set; }
            public Point[] Corners { get; set; }
            public double WidthPx { get; set; }
            public double HeightPx { get; set; }
            public double Confidence { get; set; }
            public string Error { get; set; }
        }

        public static void TestOpenCVWithDebug(string imagePath)
        {
            Console.WriteLine($"\n🔍 ТЕСТИРУЕМ ЭВРИСТИЧЕСКИЙ МЕТОД НА ФАЙЛЕ: {imagePath}");

            if (!File.Exists(imagePath))
            {
                Console.WriteLine("❌ Файл не найден!");
                return;
            }

            try
            {
                // Создаем папку для debug-изображений
                string debugFolder = Path.Combine(Path.GetTempPath(), "HeuristicDetection", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                Directory.CreateDirectory(debugFolder);
                Console.WriteLine($"📁 Debug папка: {debugFolder}");

                // 1. Загружаем исходное изображение
                using var image = Cv2.ImRead(imagePath);
                Console.WriteLine($"✅ Изображение загружено: {image.Width} x {image.Height}");
                SaveDebugImage(image, debugFolder, "01_original.jpg");

                // 2. Применяем эвристический метод
                var result = HeuristicStoneDetection(image, debugFolder);

                // 3. Выводим результат
                if (result.Success)
                {
                    Console.WriteLine($"\n🎯 УСПЕШНАЯ ДЕТЕКЦИЯ!");
                    Console.WriteLine($"   Размеры: {result.WidthPx:F1} x {result.HeightPx:F1} px");
                    Console.WriteLine($"   Уверенность: {result.Confidence:P1}");
                    Console.WriteLine($"   Углы:");
                    for (int i = 0; i < result.Corners.Length; i++)
                    {
                        Console.WriteLine($"   [{i}] ({result.Corners[i].X}, {result.Corners[i].Y})");
                    }

                    // Рисуем финальный результат
                    DrawFinalResult(image, result, debugFolder);
                }
                else
                {
                    Console.WriteLine($"❌ Детекция не удалась: {result.Error}");

                    // Пробуем запасной метод
                    Console.WriteLine("\n🔄 Пробуем запасной метод...");
                    var fallbackResult = FallbackDetection(image, debugFolder);

                    if (fallbackResult.Success)
                    {
                        DrawFinalResult(image, fallbackResult, debugFolder, "12_fallback_result.jpg");
                    }
                }

                Console.WriteLine($"\n✅ Все debug изображения сохранены в: {debugFolder}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        /// <summary>
        /// Эвристический метод определения границ каменной плиты
        /// </summary>
        private static DetectionResult HeuristicStoneDetection(Mat image, string debugFolder)
        {
            var result = new DetectionResult { Success = false };

            try
            {
                // 1. Конвертируем в оттенки серого
                using var gray = new Mat();
                Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
                SaveDebugImage(gray, debugFolder, "02_grayscale.jpg");

                // 2. Анализируем углы изображения (там обычно фон)
                Console.WriteLine("\n🔍 АНАЛИЗ ФОНА ПО УГЛАМ:");
                var backgroundInfo = AnalyzeBackground(gray, debugFolder);

                if (backgroundInfo == null)
                {
                    result.Error = "Не удалось проанализировать фон";
                    return result;
                }

                // 3. Сканируем от каждого края к центру
                Console.WriteLine("\n📏 СКАНИРОВАНИЕ ГРАНИЦ ОТ КРАЕВ:");
                var boundaries = ScanBoundaries(gray, backgroundInfo, debugFolder);

                // 4. Проверяем, нашли ли все 4 границы
                if (boundaries.Top == -1 || boundaries.Bottom == -1 ||
                    boundaries.Left == -1 || boundaries.Right == -1)
                {
                    result.Error = "Не удалось найти все границы";
                    return result;
                }

                Console.WriteLine($"   Верхняя граница: {boundaries.Top}");
                Console.WriteLine($"   Нижняя граница: {boundaries.Bottom}");
                Console.WriteLine($"   Левая граница: {boundaries.Left}");
                Console.WriteLine($"   Правая граница: {boundaries.Right}");

                // 5. Формируем углы прямоугольника
                var corners = new Point[]
                {
                    new Point(boundaries.Left, boundaries.Top),       // верхний-левый
                    new Point(boundaries.Right, boundaries.Top),      // верхний-правый
                    new Point(boundaries.Right, boundaries.Bottom),   // нижний-правый
                    new Point(boundaries.Left, boundaries.Bottom)     // нижний-левый
                };

                // 6. Вычисляем размеры
                double width = boundaries.Right - boundaries.Left;
                double height = boundaries.Bottom - boundaries.Top;

                // 7. Оцениваем уверенность
                double confidence = CalculateHeuristicConfidence(gray, boundaries, backgroundInfo);

                result.Success = true;
                result.Corners = corners;
                result.WidthPx = width;
                result.HeightPx = height;
                result.Confidence = confidence;

                // 8. Визуализируем процесс сканирования
                VisualizeScanning(image, boundaries, debugFolder);

                return result;
            }
            catch (Exception ex)
            {
                result.Error = $"Ошибка в эвристическом методе: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// Анализ фона по углам изображения
        /// </summary>
        private class BackgroundInfo
        {
            public double MeanBrightness { get; set; }
            public double StdDev { get; set; }
            public double Threshold { get; set; }
            public Point[] CornerPoints { get; set; }
        }

        private static BackgroundInfo AnalyzeBackground(Mat gray, string debugFolder)
        {
            int h = gray.Height;
            int w = gray.Width;
            int sampleSize = Math.Min(50, Math.Min(w / 5, h / 5));

            var corners = new Point[]
            {
                new Point(sampleSize, sampleSize),
                new Point(w - sampleSize * 2, sampleSize),
                new Point(sampleSize, h - sampleSize * 2),
                new Point(w - sampleSize * 2, h - sampleSize * 2)
            };

            var samples = new List<byte>();

            // ИСПРАВЛЕНО: сначала создаем Mat, потом конвертируем
            using var cornerVisual = new Mat();
            Cv2.CvtColor(gray, cornerVisual, ColorConversionCodes.GRAY2BGR);

            foreach (var corner in corners)
            {
                Cv2.Rectangle(cornerVisual,
                    new Rect(corner.X, corner.Y, sampleSize, sampleSize),
                    Scalar.Red, 2);

                for (int y = corner.Y; y < corner.Y + sampleSize && y < gray.Height; y++)
                {
                    for (int x = corner.X; x < corner.X + sampleSize && x < gray.Width; x++)
                    {
                        samples.Add(gray.At<byte>(y, x));
                    }
                }
            }

            SaveDebugImage(cornerVisual, debugFolder, "03_background_samples.jpg");

            if (samples.Count == 0)
                return null;

            // Вычисляем статистику фона
            double mean = samples.Average(b => b);
            double variance = samples.Select(b => Math.Pow(b - mean, 2)).Average();
            double stdDev = Math.Sqrt(variance);

            // Порог для определения "не фона" (среднее + 2*стандартное отклонение)
            // Для темного фона используем меньший порог, для светлого - больший
            double threshold = mean + (mean > 128 ? -2 : 2) * stdDev;

            Console.WriteLine($"   Средняя яркость фона: {mean:F1}");
            Console.WriteLine($"   Стандартное отклонение: {stdDev:F1}");
            Console.WriteLine($"   Порог: {threshold:F1}");

            return new BackgroundInfo
            {
                MeanBrightness = mean,
                StdDev = stdDev,
                Threshold = threshold,
                CornerPoints = corners
            };
        }

        /// <summary>
        /// Результат сканирования границ
        /// </summary>
        private class Boundaries
        {
            public int Top = -1;
            public int Bottom = -1;
            public int Left = -1;
            public int Right = -1;
        }

        private static Boundaries ScanBoundaries(Mat gray, BackgroundInfo bgInfo, string debugFolder)
        {
            var boundaries = new Boundaries();
            int h = gray.Height;
            int w = gray.Width;

            // Параметры сканирования
            int scanStep = 5; // шаг сканирования для производительности
            int windowSize = 10; // размер окна для усреднения
            double changeThreshold = bgInfo.StdDev * 2.5; // порог изменения яркости

            // Создаем изображение для визуализации сканирования
            Mat scanVisual = new Mat();  // сначала создаем пустой Mat
            Cv2.CvtColor(gray, scanVisual, ColorConversionCodes.GRAY2BGR); 

            // 1. СКАНИРОВАНИЕ СВЕРХУ ВНИЗ (поиск верхней границы)
            Console.WriteLine("   Сканирование сверху вниз...");
            for (int y = windowSize; y < h - windowSize; y += scanStep)
            {
                bool isBackground = true;
                double avgBrightness = 0;

                // Проверяем несколько точек по горизонтали
                for (int x = windowSize; x < w - windowSize; x += scanStep * 2)
                {
                    // Усредняем яркость в маленьком окне
                    double localMean = 0;
                    for (int dy = -windowSize / 2; dy <= windowSize / 2; dy++)
                    {
                        for (int dx = -windowSize / 2; dx <= windowSize / 2; dx++)
                        {
                            localMean += gray.At<byte>(y + dy, x + dx);
                        }
                    }
                    localMean /= (windowSize * windowSize);
                    avgBrightness += localMean;
                }
                avgBrightness /= (w / (scanStep * 2));

                // Рисуем точку сканирования
                Cv2.Circle(scanVisual, w / 2, y, 3, Scalar.Blue, -1);

                // Проверяем, отличается ли от фона
                if (Math.Abs(avgBrightness - bgInfo.MeanBrightness) > changeThreshold)
                {
                    boundaries.Top = y - windowSize;
                    Cv2.Line(scanVisual, 0, y, w, y, Scalar.Green, 2);
                    Console.WriteLine($"   ✅ Верхняя граница найдена на y={y}");
                    break;
                }
            }

            // 2. СКАНИРОВАНИЕ СНИЗУ ВВЕРХ (поиск нижней границы)
            Console.WriteLine("   Сканирование снизу вверх...");
            for (int y = h - windowSize; y > windowSize; y -= scanStep)
            {
                bool isBackground = true;
                double avgBrightness = 0;

                for (int x = windowSize; x < w - windowSize; x += scanStep * 2)
                {
                    double localMean = 0;
                    for (int dy = -windowSize / 2; dy <= windowSize / 2; dy++)
                    {
                        for (int dx = -windowSize / 2; dx <= windowSize / 2; dx++)
                        {
                            localMean += gray.At<byte>(y + dy, x + dx);
                        }
                    }
                    localMean /= (windowSize * windowSize);
                    avgBrightness += localMean;
                }
                avgBrightness /= (w / (scanStep * 2));

                Cv2.Circle(scanVisual, w / 2, y, 3, Scalar.Blue, -1);

                if (Math.Abs(avgBrightness - bgInfo.MeanBrightness) > changeThreshold)
                {
                    boundaries.Bottom = y + windowSize;
                    Cv2.Line(scanVisual, 0, y, w, y, Scalar.Green, 2);
                    Console.WriteLine($"   ✅ Нижняя граница найдена на y={y}");
                    break;
                }
            }

            // 3. СКАНИРОВАНИЕ СЛЕВА НАПРАВО (поиск левой границы)
            Console.WriteLine("   Сканирование слева направо...");
            for (int x = windowSize; x < w - windowSize; x += scanStep)
            {
                double avgBrightness = 0;

                for (int y = windowSize; y < h - windowSize; y += scanStep * 2)
                {
                    double localMean = 0;
                    for (int dy = -windowSize / 2; dy <= windowSize / 2; dy++)
                    {
                        for (int dx = -windowSize / 2; dx <= windowSize / 2; dx++)
                        {
                            localMean += gray.At<byte>(y + dy, x + dx);
                        }
                    }
                    localMean /= (windowSize * windowSize);
                    avgBrightness += localMean;
                }
                avgBrightness /= (h / (scanStep * 2));

                Cv2.Circle(scanVisual, x, h / 2, 3, Scalar.Blue, -1);

                if (Math.Abs(avgBrightness - bgInfo.MeanBrightness) > changeThreshold)
                {
                    boundaries.Left = x - windowSize;
                    Cv2.Line(scanVisual, x, 0, x, h, Scalar.Green, 2);
                    Console.WriteLine($"   ✅ Левая граница найдена на x={x}");
                    break;
                }
            }

            // 4. СКАНИРОВАНИЕ СПРАВА НАЛЕВО (поиск правой границы)
            Console.WriteLine("   Сканирование справа налево...");
            for (int x = w - windowSize; x > windowSize; x -= scanStep)
            {
                double avgBrightness = 0;

                for (int y = windowSize; y < h - windowSize; y += scanStep * 2)
                {
                    double localMean = 0;
                    for (int dy = -windowSize / 2; dy <= windowSize / 2; dy++)
                    {
                        for (int dx = -windowSize / 2; dx <= windowSize / 2; dx++)
                        {
                            localMean += gray.At<byte>(y + dy, x + dx);
                        }
                    }
                    localMean /= (windowSize * windowSize);
                    avgBrightness += localMean;
                }
                avgBrightness /= (h / (scanStep * 2));

                Cv2.Circle(scanVisual, x, h / 2, 3, Scalar.Blue, -1);

                if (Math.Abs(avgBrightness - bgInfo.MeanBrightness) > changeThreshold)
                {
                    boundaries.Right = x + windowSize;
                    Cv2.Line(scanVisual, x, 0, x, h, Scalar.Green, 2);
                    Console.WriteLine($"   ✅ Правая граница найдена на x={x}");
                    break;
                }
            }

            SaveDebugImage(scanVisual, debugFolder, "04_scanning_process.jpg");
            scanVisual.Dispose();
            return boundaries;
        }

        /// <summary>
        /// Визуализация процесса сканирования
        /// </summary>
        private static void VisualizeScanning(Mat image, Boundaries boundaries, string debugFolder)
        {
            using var visual = image.Clone();

            // Рисуем найденные границы
            if (boundaries.Top != -1)
                Cv2.Line(visual, 0, boundaries.Top, image.Width, boundaries.Top, Scalar.Green, 2);

            if (boundaries.Bottom != -1)
                Cv2.Line(visual, 0, boundaries.Bottom, image.Width, boundaries.Bottom, Scalar.Green, 2);

            if (boundaries.Left != -1)
                Cv2.Line(visual, boundaries.Left, 0, boundaries.Left, image.Height, Scalar.Green, 2);

            if (boundaries.Right != -1)
                Cv2.Line(visual, boundaries.Right, 0, boundaries.Right, image.Height, Scalar.Green, 2);

            // Рисуем предполагаемый прямоугольник
            if (boundaries.Top != -1 && boundaries.Bottom != -1 &&
                boundaries.Left != -1 && boundaries.Right != -1)
            {
                var rect = new Rect(
                    boundaries.Left,
                    boundaries.Top,
                    boundaries.Right - boundaries.Left,
                    boundaries.Bottom - boundaries.Top);

                Cv2.Rectangle(visual, rect, Scalar.Red, 3);
            }

            SaveDebugImage(visual, debugFolder, "05_detected_boundaries.jpg");
        }

        /// <summary>
        /// Расчет уверенности в найденных границах
        /// </summary>
        private static double CalculateHeuristicConfidence(Mat gray, Boundaries boundaries, BackgroundInfo bgInfo)
        {
            double confidence = 1.0;

            // 1. Проверяем, что внутри прямоугольника яркость отличается от фона
            int insideSamples = 0;
            int differentPixels = 0;

            for (int y = boundaries.Top + 10; y < boundaries.Bottom - 10 && y < gray.Height; y += 20)
            {
                for (int x = boundaries.Left + 10; x < boundaries.Right - 10 && x < gray.Width; x += 20)
                {
                    insideSamples++;
                    byte pixel = gray.At<byte>(y, x);
                    if (Math.Abs(pixel - bgInfo.MeanBrightness) > bgInfo.StdDev * 2)
                        differentPixels++;
                }
            }

            double insideRatio = insideSamples > 0 ? (double)differentPixels / insideSamples : 0;
            confidence *= insideRatio; // чем больше пикселей отличаются от фона, тем лучше

            // 2. Проверяем пропорции (плита не должна быть слишком вытянутой)
            double width = boundaries.Right - boundaries.Left;
            double height = boundaries.Bottom - boundaries.Top;
            double aspectRatio = Math.Max(width, height) / Math.Min(width, height);

            if (aspectRatio > 5) // слишком вытянутая
                confidence *= 0.5;
            else if (aspectRatio > 3)
                confidence *= 0.8;

            // 3. Проверяем размер (плита должна занимать значительную часть изображения)
            double imageArea = gray.Width * gray.Height;
            double plateArea = width * height;
            double areaRatio = plateArea / imageArea;

            if (areaRatio < 0.2) // слишком маленькая
                confidence *= 0.5;
            else if (areaRatio < 0.3)
                confidence *= 0.8;

            return Math.Min(1.0, Math.Max(0, confidence));
        }

        /// <summary>
        /// Запасной метод детекции
        /// </summary>
        private static DetectionResult FallbackDetection(Mat image, string debugFolder)
        {
            Console.WriteLine("   Используем метод пороговой обработки...");

            using var gray = new Mat();
            Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);

            // Пробуем разные пороги
            for (int thresh = 30; thresh <= 200; thresh += 30)
            {
                using var binary = new Mat();
                Cv2.Threshold(gray, binary, thresh, 255, ThresholdTypes.Binary);

                Cv2.FindContours(binary, out Point[][] contours, out HierarchyIndex[] hierarchy,
                    RetrievalModes.External, ContourApproximationModes.ApproxSimple);

                if (contours.Length > 0)
                {
                    // Берем самый большой контур
                    var largest = contours.OrderByDescending(c => Cv2.ContourArea(c)).First();
                    var rect = Cv2.BoundingRect(largest);

                    // Проверяем, что контур достаточно большой
                    if (rect.Width * rect.Height > image.Width * image.Height * 0.1)
                    {
                        var corners = new Point[]
                        {
                            new Point(rect.Left, rect.Top),
                            new Point(rect.Right, rect.Top),
                            new Point(rect.Right, rect.Bottom),
                            new Point(rect.Left, rect.Bottom)
                        };

                        return new DetectionResult
                        {
                            Success = true,
                            Corners = corners,
                            WidthPx = rect.Width,
                            HeightPx = rect.Height,
                            Confidence = 0.6
                        };
                    }
                }
            }

            return new DetectionResult { Success = false, Error = "Запасной метод не сработал" };
        }

        /// <summary>
        /// Рисование финального результата
        /// </summary>
        private static void DrawFinalResult(Mat image, DetectionResult result, string debugFolder, string filename = "11_final_result.jpg")
        {
            using var finalImage = image.Clone();

            // Рисуем прямоугольник
            for (int i = 0; i < 4; i++)
            {
                Cv2.Line(finalImage, result.Corners[i], result.Corners[(i + 1) % 4], Scalar.Green, 3);
            }

            // Рисуем углы
            foreach (var p in result.Corners)
            {
                Cv2.Circle(finalImage, p, 8, Scalar.Red, -1);
                Cv2.PutText(finalImage, $"({p.X}, {p.Y})",
                    new Point(p.X + 10, p.Y - 10),
                    HersheyFonts.HersheySimplex, 0.5, Scalar.Blue, 1);
            }

            // Добавляем размеры
            var center = new Point(
                (int)result.Corners.Average(p => p.X),
                (int)result.Corners.Average(p => p.Y));

            Cv2.PutText(finalImage, $"W: {result.WidthPx:F1} px",
                new Point(center.X - 80, center.Y - 20),
                HersheyFonts.HersheySimplex, 0.7, Scalar.Yellow, 2);

            Cv2.PutText(finalImage, $"H: {result.HeightPx:F1} px",
                new Point(center.X - 80, center.Y + 10),
                HersheyFonts.HersheySimplex, 0.7, Scalar.Yellow, 2);

            Cv2.PutText(finalImage, $"Conf: {result.Confidence:P0}",
                new Point(center.X - 80, center.Y + 40),
                HersheyFonts.HersheySimplex, 0.6, Scalar.Yellow, 2);

            SaveDebugImage(finalImage, debugFolder, filename);
        }

        private static void SaveDebugImage(Mat image, string debugFolder, string filename)
        {
            string path = Path.Combine(debugFolder, filename);
            Cv2.ImWrite(path, image);
            Console.WriteLine($"   💾 Сохранено: {filename}");
        }
    }
}