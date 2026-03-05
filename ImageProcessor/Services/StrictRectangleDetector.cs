using OpenCvSharp;

namespace ImageProcessor.Services
{
    public class StrictRectangleDetector
    {
        public static DetectionResult FindMainRectangle(string imagePath)
        {
            Console.WriteLine($"\n🎯 СТРОГИЙ ПОИСК ПРЯМОУГОЛЬНИКА");
            Console.WriteLine($"   Файл: {Path.GetFileName(imagePath)}");

            var result = new DetectionResult();

            using var image = Cv2.ImRead(imagePath);
            using var resized = ResizeImage(image, 800);
            result.OriginalSize = new Size(image.Width, image.Height);
            result.ProcessedSize = new Size(resized.Width, resized.Height);

            try
            {
                // ШАГ 1: Предобработка
                using var processed = PreprocessForRectangles(resized, imagePath);

                // ШАГ 2: Находим ВСЕ контуры
                var allContours = Cv2.FindContoursAsArray(
                    processed,
                    RetrievalModes.External,
                    ContourApproximationModes.ApproxSimple
                );

                Console.WriteLine($"   Всего контуров: {allContours.Length}");

                // ШАГ 3: Фильтруем - ищем только прямоугольники
                var rectangleCandidates = FindRectangleCandidates(allContours, resized);

                Console.WriteLine($"   Кандидатов-прямоугольников: {rectangleCandidates.Count}");

                if (rectangleCandidates.Count == 0)
                {
                    result.Error = "Не найдено ни одного прямоугольника";
                    return result;
                }

                // ШАГ 4: Выбираем ГЛАВНЫЙ прямоугольник
                var mainRectangle = SelectMainRectangle(rectangleCandidates, resized);

                if (mainRectangle == null)
                {
                    result.Error = "Не удалось выбрать главный прямоугольник";
                    return result;
                }

                // ШАГ 5: Получаем 4 угла прямоугольника
                var corners = GetRectangleCorners(mainRectangle.Value);

                // ШАГ 6: Масштабируем углы обратно к оригинальному размеру
                var scaleX = (double)image.Width / resized.Width;
                var scaleY = (double)image.Height / resized.Height;

                result.Corners = corners.Select(p =>
                    new Point((int)(p.X * scaleX), (int)(p.Y * scaleY))
                ).ToArray();

                result.Rectangle = mainRectangle.Value;
                result.Success = true;

                // ШАГ 7: Визуализируем результат
                VisualizeResult(resized, allContours, rectangleCandidates, mainRectangle.Value, corners, imagePath);

                Console.WriteLine($"   ✅ ГЛАВНЫЙ ПРЯМОУГОЛЬНИК НАЙДЕН");
                Console.WriteLine($"      Размер: {mainRectangle.Value.Size.Width:F0}x{mainRectangle.Value.Size.Height:F0} px");
                Console.WriteLine($"      Угол: {mainRectangle.Value.Angle:F1}°");
                Console.WriteLine($"      Центр: ({mainRectangle.Value.Center.X:F0}, {mainRectangle.Value.Center.Y:F0})");

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ Ошибка: {ex.Message}");
                result.Error = ex.Message;
                return result;
            }
        }



        private static Mat PreprocessForRectangles(Mat image, string basePath)
        {
            // 1. В grayscale
            using var gray = new Mat();
            Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);

            // 2. Убираем шум
            using var blurred = new Mat();
            Cv2.GaussianBlur(gray, blurred, new Size(5, 5), 0);

            // 3. Adaptive threshold - лучше для неравномерного освещения
            using var binary = new Mat();
            Cv2.AdaptiveThreshold(blurred, binary, 255,
                AdaptiveThresholdTypes.GaussianC,
                ThresholdTypes.Binary, 11, 2);

            // 4. Морфологические операции для соединения разрывов
            using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
            Cv2.MorphologyEx(binary, binary, MorphTypes.Close, kernel, iterations: 2);
            Cv2.MorphologyEx(binary, binary, MorphTypes.Open, kernel, iterations: 1);

            // Сохраняем для отладки
            Cv2.ImWrite(basePath.Replace(".jpg", "_preprocessed.jpg"), binary);

            return binary.Clone();
        }

        private static List<RotatedRect> FindRectangleCandidates(Point[][] contours, Mat image)
        {
            var candidates = new List<RotatedRect>();
            var minArea = image.Width * image.Height * 0.01; // Не меньше 1% изображения
            var maxArea = image.Width * image.Height * 0.95; // Не больше 95%

            foreach (var contour in contours)
            {
                var area = Cv2.ContourArea(contour);

                // Фильтр по площади
                if (area < minArea || area > maxArea) continue;

                // Аппроксимируем контур
                var epsilon = 0.02 * Cv2.ArcLength(contour, true);
                var approx = Cv2.ApproxPolyDP(contour, epsilon, true);

                // Нас интересуют только 4-угольники
                if (approx.Length != 4) continue;

                // Проверяем, что это выпуклый многоугольник
                if (!Cv2.IsContourConvex(approx)) continue;

                // Получаем минимальный ограничивающий прямоугольник
                var rect = Cv2.MinAreaRect(contour);

                // Фильтр по соотношению сторон (слишком вытянутые отбрасываем)
                var aspectRatio = rect.Size.Width / rect.Size.Height;
                if (aspectRatio < 0.2 || aspectRatio > 5.0) continue;

                // Проверяем углы прямоугольника (должны быть ~90 градусов)
                if (!HasRightAngles(approx, 20)) continue; // Допуск 20 градусов

                candidates.Add(rect);
            }

            return candidates;
        }

        private static bool HasRightAngles(Point[] corners, double toleranceDegrees)
        {
            if (corners.Length != 4) return false;

            // Вычисляем углы между соседними сторонами
            for (int i = 0; i < 4; i++)
            {
                var p1 = corners[i];
                var p2 = corners[(i + 1) % 4];
                var p3 = corners[(i + 2) % 4];

                var angle = CalculateAngle(p1, p2, p3);
                var diff = Math.Abs(90 - angle);

                if (diff > toleranceDegrees)
                    return false;
            }

            return true;
        }

        private static double CalculateAngle(Point p1, Point p2, Point p3)
        {
            // Угол в вершине p2 между векторами p1->p2 и p2->p3
            var v1 = new Point(p1.X - p2.X, p1.Y - p2.Y);
            var v2 = new Point(p3.X - p2.X, p3.Y - p2.Y);

            var dot = v1.X * v2.X + v1.Y * v2.Y;
            var norm1 = Math.Sqrt(v1.X * v1.X + v1.Y * v1.Y);
            var norm2 = Math.Sqrt(v2.X * v2.X + v2.Y * v2.Y);

            var cos = dot / (norm1 * norm2);
            cos = Math.Max(-1, Math.Min(1, cos)); // Ограничиваем [-1, 1]

            return Math.Acos(cos) * (180 / Math.PI);
        }

        private static RotatedRect? SelectMainRectangle(List<RotatedRect> candidates, Mat image)
        {
            if (candidates.Count == 0) return null;

            // Стратегия 1: Самый большой по площади
            var byArea = candidates.OrderByDescending(r => r.Size.Width * r.Size.Height).First();

            // Стратегия 2: Ближайший к центру изображения
            var imageCenter = new Point2f(image.Width / 2f, image.Height / 2f);
            var byCenter = candidates.OrderBy(r => Distance(r.Center, imageCenter)).First();

            // Стратегия 3: С наименьшим углом поворота (ближе к горизонтали/вертикали)
            var byAngle = candidates.OrderBy(r => Math.Min(Math.Abs(r.Angle), Math.Abs(r.Angle - 90))).First();

            // Комбинированная оценка
            var scores = candidates.Select(r => new
            {
                Rect = r,
                Score = CalculateRectangleScore(r, image)
            }).ToList();

            var best = scores.OrderByDescending(s => s.Score).First();

            Console.WriteLine($"   Выбран прямоугольник с оценкой: {best.Score:F2}");

            return best.Rect;
        }

        private static float CalculateRectangleScore(RotatedRect rect, Mat image)
        {
            float score = 0;

            // 1. Площадь (чем больше - тем лучше, но не слишком)
            var area = rect.Size.Width * rect.Size.Height;
            var imageArea = image.Width * image.Height;
            var areaRatio = area / imageArea;

            // Идеально 30-70% изображения
            if (areaRatio > 0.3 && areaRatio < 0.7) score += 3;
            else if (areaRatio > 0.2 && areaRatio < 0.8) score += 2;
            else if (areaRatio > 0.1 && areaRatio < 0.9) score += 1;

            // 2. Близость к центру
            var imageCenter = new Point2f(image.Width / 2f, image.Height / 2f);
            var distance = Distance(rect.Center, imageCenter);
            var maxDistance = Math.Sqrt(image.Width * image.Width + image.Height * image.Height) / 2;
            var centerScore = 1 - (distance / maxDistance);
            score += (float)centerScore * 2;

            // 3. Угол (чем ближе к 0 или 90 - тем лучше)
            var angle = Math.Min(Math.Abs(rect.Angle), Math.Abs(rect.Angle - 90));
            var angleScore = 1 - (angle / 45); // 45 - максимальный допустимый
            score += (float)Math.Max(0, angleScore);

            // 4. Соотношение сторон (близко к "стандартным" пропорциям)
            var aspectRatio = rect.Size.Width / rect.Size.Height;
            if (aspectRatio < 1) aspectRatio = 1 / aspectRatio; // Всегда >= 1

            // Идеальные пропорции для плит
            var idealRatios = new[] { 1.0, 1.25, 1.33, 1.5, 1.75, 2.0 };
            var bestMatch = idealRatios.Min(r => Math.Abs(aspectRatio - r));
            var ratioScore = 1 - (bestMatch / 1.0); // Допуск до 1.0
            score += (float)Math.Max(0, ratioScore);

            return score;
        }

        private static Point[] GetRectangleCorners(RotatedRect rect)
        {
            var points = Cv2.BoxPoints(rect);
            return points.Select(p => new Point((int)p.X, (int)p.Y)).ToArray();
        }

        private static void VisualizeResult(Mat image, Point[][] allContours,
            List<RotatedRect> candidates, RotatedRect mainRect, Point[] corners, string basePath)
        {
            using var result = image.Clone();

            // 1. Все контуры (серым)
            foreach (var contour in allContours)
            {
                Cv2.DrawContours(result, new[] { contour }, -1, new Scalar(100, 100, 100), 1);
            }

            // 2. Кандидаты-прямоугольники (зелёным)
            foreach (var rect in candidates)
            {
                var points = Cv2.BoxPoints(rect);
                for (int i = 0; i < 4; i++)
                {
                    Cv2.Line(result, (Point)points[i], (Point)points[(i + 1) % 4],
                        new Scalar(0, 200, 0), 1);
                }
            }

            // 3. Главный прямоугольник (красным)
            var mainPoints = Cv2.BoxPoints(mainRect);
            for (int i = 0; i < 4; i++)
            {
                Cv2.Line(result, (Point)mainPoints[i], (Point)mainPoints[(i + 1) % 4],
                    Scalar.Red, 3);
            }

            // 4. Углы (синие кружки)
            foreach (var corner in corners)
            {
                Cv2.Circle(result, corner, 8, Scalar.Blue, -1);
                Cv2.Circle(result, corner, 8, Scalar.White, 2);
            }

            // 5. Центр (жёлтый кружок)
            Cv2.Circle(result, (Point)mainRect.Center, 6, Scalar.Yellow, -1);

            // 6. Подписи
            Cv2.PutText(result, "MAIN OBJECT",
                new Point(20, 40),
                HersheyFonts.HersheySimplex, 1.2, Scalar.Red, 3);

            var info = $"{mainRect.Size.Width:F0} x {mainRect.Size.Height:F0} px";
            Cv2.PutText(result, info,
                new Point(20, 80),
                HersheyFonts.HersheySimplex, 0.8, Scalar.White, 2);

            Cv2.ImWrite(basePath.Replace(".jpg", "_strict_rectangles.jpg"), result);

            // 7. Чистый результат (только главный прямоугольник)
            using var cleanResult = image.Clone();
            for (int i = 0; i < 4; i++)
            {
                Cv2.Line(cleanResult, (Point)mainPoints[i], (Point)mainPoints[(i + 1) % 4],
                    Scalar.Red, 3);
            }
            foreach (var corner in corners)
            {
                Cv2.Circle(cleanResult, corner, 10, Scalar.Blue, -1);
                Cv2.Circle(cleanResult, corner, 10, Scalar.White, 2);
            }

            Cv2.ImWrite(basePath.Replace(".jpg", "_main_rectangle_only.jpg"), cleanResult);
        }

        private static Mat ResizeImage(Mat image, int maxWidth)
        {
            if (image.Width <= maxWidth) return image.Clone();

            double scale = (double)maxWidth / image.Width;
            var newSize = new Size(maxWidth, (int)(image.Height * scale));

            var resized = new Mat();
            Cv2.Resize(image, resized, newSize);

            return resized;
        }

        private static double Distance(Point2f p1, Point2f p2)
        {
            var dx = p1.X - p2.X;
            var dy = p1.Y - p2.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }



    public class DetectionResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public Point[] Corners { get; set; } = new Point[4];
        public RotatedRect? Rectangle { get; set; }
        public Size OriginalSize { get; set; }
        public Size ProcessedSize { get; set; }
    }
}