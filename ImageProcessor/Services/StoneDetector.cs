using OpenCvSharp;

namespace ImageProcessor.Services
{
    public class StoneDetector
    {
         //public static void DetectStoneSlab(string imagePath)
         //{
         //    Console.WriteLine($"\n🪨 Ищем каменную плиту: {Path.GetFileName(imagePath)}");
         //
         //    using var image = Cv2.ImRead(imagePath);
         //
         //    // 1. Уменьшаем размер для скорости
         //    using var resized = ResizeImage(image, 800);
         //
         //    // 2. Метод 1: Поиск по текстуре (для камня)
         //    Console.WriteLine("🔍 Метод 1: Поиск по текстуре...");
         //    TryFindByTexture(resized, imagePath);
         //
         //    // 3. Метод 2: Поиск по градиенту
         //    Console.WriteLine("\n🔍 Метод 2: Поиск по градиенту...");
         //    TryFindByGradient(resized, imagePath);
         //
         //    // 4. Метод 3: Поиск крупных прямоугольных областей
         //    Console.WriteLine("\n🔍 Метод 3: Поиск прямоугольных областей...");
         //    TryFindLargeRectangles(resized, imagePath);
         //
         //    Console.WriteLine("\n🪨 Детекция завершена");
         //}

        public static void DetectStoneSlab(string imagePath)
        {
            Console.WriteLine($"\n🪨 Ищем каменную плиту: {Path.GetFileName(imagePath)}");
        
            using var image = Cv2.ImRead(imagePath);
            using var resized = ResizeImage(image, 800);
        
            // 1. Старые методы для сравнения
            Console.WriteLine("🔍 Метод 1: Поиск по текстуре...");
            TryFindByTexture(resized, imagePath);
        
            // 2. НОВЫЙ улучшенный метод
            Console.WriteLine("\n🔵 Метод 2: Улучшенная детекция...");
            DetectStoneWithEdges(imagePath);
        
            Console.WriteLine("\n🪨 Детекция завершена");
        }

        public static void DetectStoneWithEdges(string imagePath)
        {
            Console.WriteLine($"\n🔵 Улучшенная детекция камня");

            using var image = Cv2.ImRead(imagePath);
            using var resized = ResizeImage(image, 800);

            // 1. Получаем текстуру (как раньше)
            using var gray = new Mat();
            Cv2.CvtColor(resized, gray, ColorConversionCodes.BGR2GRAY);

            using var blurred = new Mat();
            Cv2.GaussianBlur(gray, blurred, new Size(15, 15), 0);

            using var diff = new Mat();
            Cv2.Absdiff(gray, blurred, diff);

            using var textureMask = new Mat();
            Cv2.Threshold(diff, textureMask, 15, 255, ThresholdTypes.Binary); // Порог 15 вместо 10

            // 2. Улучшаем маску
            using var improvedMask = ImproveMask(textureMask);

            // 3. Находим самый большой контур в улучшенной маске
            var contours = Cv2.FindContoursAsArray(
                improvedMask,
                RetrievalModes.External,
                ContourApproximationModes.ApproxSimple
            );

            if (contours.Length == 0)
            {
                Console.WriteLine("   ❌ Контуры не найдены");
                return;
            }

            var largestContour = contours.OrderByDescending(c => Cv2.ContourArea(c)).First();

            // 4. Уточняем границы с помощью Canny на ОРИГИНАЛЬНОМ изображении
            using var edges = new Mat();
            Cv2.Canny(gray, edges, 50, 150);

            // 5. Комбинируем текстуру и границы
            using var combined = new Mat();
            Cv2.BitwiseOr(improvedMask, edges, combined);

            // 6. Находим контур в комбинированном изображении
            var finalContours = Cv2.FindContoursAsArray(
                combined,
                RetrievalModes.External,
                ContourApproximationModes.ApproxSimple
            );

            if (finalContours.Length == 0)
            {
                Console.WriteLine("   ❌ Финальные контуры не найдены");
                return;
            }

            var finalContour = finalContours.OrderByDescending(c => Cv2.ContourArea(c)).First();

            // 7. Аппроксимируем контур (упрощаем до многоугольника)
            var epsilon = 0.01 * Cv2.ArcLength(finalContour, true);
            var approx = Cv2.ApproxPolyDP(finalContour, epsilon, true);

            Console.WriteLine($"   Углов в контуре: {approx.Length}");

            // 8. Рисуем результат
            using var result = resized.Clone();

            // Заливаем область плиты полупрозрачным цветом
            using var fill = new Mat(result.Size(), MatType.CV_8UC3, new Scalar(0, 100, 0)); // Зелёный
            Cv2.DrawContours(fill, new[] { approx }, -1, new Scalar(0, 200, 0), -1);
            Cv2.AddWeighted(result, 0.7, fill, 0.3, 0, result);

            // Рисуем границу
            Cv2.DrawContours(result, new[] { approx }, -1, Scalar.Red, 3);

            // Рисуем углы
            foreach (var point in approx)
            {
                Cv2.Circle(result, (Point)point, 8, Scalar.Blue, -1);
                Cv2.Circle(result, (Point)point, 8, Scalar.White, 2);
            }

            // 9. Если это 4-угольник (прямоугольник), вычисляем размеры
            if (approx.Length == 4)
            {
                var rect = Cv2.MinAreaRect(finalContour);
                Console.WriteLine($"   🔷 Найден прямоугольник:");
                Console.WriteLine($"      Размер: {rect.Size.Width:F0} x {rect.Size.Height:F0} px");
                Console.WriteLine($"      Угол: {rect.Angle:F1}°");
                Console.WriteLine($"      Соотношение: {rect.Size.Width / rect.Size.Height:F2}");

                // Рисуем rotated rectangle
                var points = Cv2.BoxPoints(rect);
                for (int i = 0; i < 4; i++)
                {
                    Cv2.Line(result, (Point)points[i], (Point)points[(i + 1) % 4], Scalar.Yellow, 2);
                }
            }

            // 10. Сохраняем все этапы для отладки
            Cv2.ImWrite(imagePath.Replace(".jpg", "_improved_mask.jpg"), improvedMask);
            Cv2.ImWrite(imagePath.Replace(".jpg", "_edges.jpg"), edges);
            Cv2.ImWrite(imagePath.Replace(".jpg", "_combined.jpg"), combined);
            Cv2.ImWrite(imagePath.Replace(".jpg", "_final_result.jpg"), result);

            Console.WriteLine($"   ✅ Результаты сохранены");
        }

        private static Mat ImproveMask(Mat mask)
        {
            // Морфологические операции для улучшения маски
            using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));

            using var improved = new Mat();

            // Закрытие (заполняем маленькие дыры)
            Cv2.MorphologyEx(mask, improved, MorphTypes.Close, kernel, iterations: 2);

            // Открытие (убираем маленькие белые точки)
            Cv2.MorphologyEx(improved, improved, MorphTypes.Open, kernel, iterations: 1);

            return improved.Clone();
        }

        private static Mat ResizeImage(Mat image, int maxWidth)
        {
            if (image.Width <= maxWidth) return image.Clone();

            double scale = (double)maxWidth / image.Width;
            var newSize = new Size(maxWidth, (int)(image.Height * scale));

            var resized = new Mat();
            Cv2.Resize(image, resized, newSize);

            Console.WriteLine($"   Изображение уменьшено: {image.Width}x{image.Height} → {resized.Width}x{resized.Height}");
            return resized;
        }

        private static void TryFindByTexture(Mat image, string basePath)
        {
            try
            {
                // Камень имеет более выраженную текстуру чем фон
                using var gray = new Mat();
                Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);

                // Вычисляем локальную дисперсию (изменчивость текстуры)
                using var blurred = new Mat();
                Cv2.GaussianBlur(gray, blurred, new Size(15, 15), 0);

                using var diff = new Mat();
                Cv2.Absdiff(gray, blurred, diff);

                // Пороговая обработка
                using var textureMask = new Mat();
                Cv2.Threshold(diff, textureMask, 10, 255, ThresholdTypes.Binary);

                // Находим крупные области текстуры
                var contours = Cv2.FindContoursAsArray(
                    textureMask,
                    RetrievalModes.External,
                    ContourApproximationModes.ApproxSimple
                );

                if (contours.Length > 0)
                {
                    var largest = contours.OrderByDescending(c => Cv2.ContourArea(c)).First();
                    var rect = Cv2.BoundingRect(largest);

                    Console.WriteLine($"   Найдена текстурная область: {rect.Width}x{rect.Height} px");

                    // Сохраняем для отладки
                    Cv2.ImWrite(basePath.Replace(".jpg", "_texture_mask.jpg"), textureMask);

                    using var result = image.Clone();
                    Cv2.Rectangle(result, rect, Scalar.Red, 2);
                    Cv2.ImWrite(basePath.Replace(".jpg", "_texture_result.jpg"), result);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   Ошибка метода текстуры: {ex.Message}");
            }
        }

        private static void TryFindByGradient(Mat image, string basePath)
        {
            try
            {
                using var gray = new Mat();
                Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);

                // Вычисляем градиенты (Sobel)
                using var gradX = new Mat();
                using var gradY = new Mat();
                Cv2.Sobel(gray, gradX, MatType.CV_16S, 1, 0);
                Cv2.Sobel(gray, gradY, MatType.CV_16S, 0, 1);

                // Конвертируем в 8-bit
                using var absGradX = new Mat();
                using var absGradY = new Mat();
                Cv2.ConvertScaleAbs(gradX, absGradX);
                Cv2.ConvertScaleAbs(gradY, absGradY);

                // Комбинируем градиенты
                using var gradient = new Mat();
                Cv2.AddWeighted(absGradX, 0.5, absGradY, 0.5, 0, gradient);

                // Порог
                using var gradientMask = new Mat();
                Cv2.Threshold(gradient, gradientMask, 30, 255, ThresholdTypes.Binary);

                // Сохраняем для отладки
                Cv2.ImWrite(basePath.Replace(".jpg", "_gradient.jpg"), gradient);
                Cv2.ImWrite(basePath.Replace(".jpg", "_gradient_mask.jpg"), gradientMask);

                Console.WriteLine($"   Градиент вычислен");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   Ошибка метода градиента: {ex.Message}");
            }
        }

        private static void TryFindLargeRectangles(Mat image, string basePath)
        {
            try
            {
                // Простой подход: ищем самые большие почти-прямоугольные области
                using var gray = new Mat();
                Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);

                // Adaptive threshold (лучше для неравномерного освещения)
                using var binary = new Mat();
                Cv2.AdaptiveThreshold(gray, binary, 255,
                    AdaptiveThresholdTypes.GaussianC,
                    ThresholdTypes.Binary, 11, 2);

                // Находим контуры
                var contours = Cv2.FindContoursAsArray(
                    binary,
                    RetrievalModes.External,
                    ContourApproximationModes.ApproxSimple
                );

                var candidates = new List<RotatedRect>();

                foreach (var contour in contours)
                {
                    var area = Cv2.ContourArea(contour);
                    if (area < image.Width * image.Height * 0.05) continue; // Меньше 5% изображения

                    var rect = Cv2.MinAreaRect(contour);
                    var aspectRatio = rect.Size.Width / rect.Size.Height;

                    // Ищем примерно прямоугольные формы
                    if (aspectRatio > 0.3 && aspectRatio < 3.0)
                    {
                        candidates.Add(rect);
                    }
                }

                Console.WriteLine($"   Найдено кандидатов: {candidates.Count}");

                if (candidates.Count > 0)
                {
                    // Выбираем самый большой
                    var largest = candidates.OrderByDescending(r => r.Size.Width * r.Size.Height).First();

                    using var result = image.Clone();
                    var points = Cv2.BoxPoints(largest);

                    for (int i = 0; i < 4; i++)
                    {
                        Cv2.Line(result, (Point)points[i], (Point)points[(i + 1) % 4], Scalar.Green, 2);
                    }

                    Cv2.ImWrite(basePath.Replace(".jpg", "_largest_rect.jpg"), result);
                    Console.WriteLine($"   Самый большой прямоугольник: {largest.Size.Width:F0}x{largest.Size.Height:F0} px");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   Ошибка метода прямоугольников: {ex.Message}");
            }
        }
    }
}