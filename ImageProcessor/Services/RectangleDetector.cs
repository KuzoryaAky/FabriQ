using OpenCvSharp;

namespace ImageProcessor.Services
{
    public class RectangleDetector
    {
        public static void FindMainObject(string imagePath)
        {
            Console.WriteLine($"\n🎯 Ищем ГЛАВНЫЙ объект на: {Path.GetFileName(imagePath)}");

            using var image = Cv2.ImRead(imagePath);

            // 1. Конвертируем в HSV (лучше для выделения по цвету)
            using var hsv = new Mat();
            Cv2.CvtColor(image, hsv, ColorConversionCodes.BGR2HSV);

            // 2. Разделяем каналы
            var hsvChannels = Cv2.Split(hsv);
            using var saturation = hsvChannels[1]; // Насыщенность

            // 3. Пороговая обработка (thresholding)
            using var binary = new Mat();
            Cv2.Threshold(saturation, binary, 50, 255, ThresholdTypes.Binary);

            // 4. Морфологические операции (убираем шум)
            using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(5, 5));
            Cv2.MorphologyEx(binary, binary, MorphTypes.Close, kernel);
            Cv2.MorphologyEx(binary, binary, MorphTypes.Open, kernel);

            // 5. Находим контуры
            var contours = Cv2.FindContoursAsArray(
                binary,
                RetrievalModes.External,
                ContourApproximationModes.ApproxSimple
            );

            Console.WriteLine($"Найдено контуров: {contours.Length}");

            if (contours.Length == 0) return;

            // 6. Выбираем самый большой контур (предполагаем что это наш объект)
            var mainContour = contours
                .OrderByDescending(c => Cv2.ContourArea(c))
                .First();

            var area = Cv2.ContourArea(mainContour);
            Console.WriteLine($"Площадь главного объекта: {area:F0} px²");

            // 7. Создаём маску (только главный объект)
            using var mask = new Mat(image.Size(), MatType.CV_8UC1, Scalar.Black);
            Cv2.DrawContours(mask, new[] { mainContour }, -1, Scalar.White, -1);

            // 8. Применяем маску к оригинальному изображению
            using var maskedImage = new Mat();
            Cv2.BitwiseAnd(image, image, maskedImage, mask);

            // 9. Находим прямоугольник для маскированного объекта
            var rect = Cv2.BoundingRect(mainContour);
            Console.WriteLine($"Bounding Box: {rect.Width} x {rect.Height} px");

            // 10. Сохраняем промежуточные результаты для отладки
            Cv2.ImWrite(imagePath.Replace(".jpg", "_hsv_saturation.jpg"), saturation);
            Cv2.ImWrite(imagePath.Replace(".jpg", "_binary_mask.jpg"), binary);
            Cv2.ImWrite(imagePath.Replace(".jpg", "_object_mask.jpg"), mask);
            Cv2.ImWrite(imagePath.Replace(".jpg", "_masked_object.jpg"), maskedImage);

            // 11. Рисуем результат на оригинале
            using var result = image.Clone();
            Cv2.Rectangle(result, rect, Scalar.Red, 3);

            // Рисуем контур объекта
            Cv2.DrawContours(result, new[] { mainContour }, -1, Scalar.Green, 2);

            // Подписываем
            Cv2.PutText(result, $"MAIN OBJECT",
                new Point(rect.X, rect.Y - 10),
                HersheyFonts.HersheySimplex, 1, Scalar.Red, 2);

            var outputPath = imagePath.Replace(".jpg", "_main_object.jpg");
            Cv2.ImWrite(outputPath, result);

            Console.WriteLine($"✅ Главный объект найден!");
            Console.WriteLine($"✅ Результат: {outputPath}");
        }

        public static void FindRectangles(string imagePath)
        {
            Console.WriteLine($"\n🔲 Ищем прямоугольники на: {Path.GetFileName(imagePath)}");

            using var image = Cv2.ImRead(imagePath);
            using var gray = new Mat();
            Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);

            // Находим контуры
            var contours = Cv2.FindContoursAsArray(
                gray,
                RetrievalModes.External,
                ContourApproximationModes.ApproxSimple
            );

            Console.WriteLine($"Всего контуров: {contours.Length}");

            var rectangles = new List<RotatedRect>();

            // Анализируем каждый контур
            for (int i = 0; i < contours.Length; i++)
            {
                var contour = contours[i];
                var area = Cv2.ContourArea(contour);

                // Пропускаем слишком маленькие
                if (area < 1000) continue;

                // Аппроксимируем контур (упрощаем)
                var epsilon = 0.02 * Cv2.ArcLength(contour, true);
                var approx = Cv2.ApproxPolyDP(contour, epsilon, true);

                // Если у контура 4 угла - это прямоугольник
                if (approx.Length == 4)
                {
                    var rect = Cv2.MinAreaRect(contour);
                    rectangles.Add(rect);

                    Console.WriteLine($"\n📐 Прямоугольник #{rectangles.Count}:");
                    Console.WriteLine($"   Площадь: {area:F0} px²");
                    Console.WriteLine($"   Размер: {rect.Size.Width:F0} x {rect.Size.Height:F0} px");
                    Console.WriteLine($"   Угол поворота: {rect.Angle:F1}°");
                    Console.WriteLine($"   Соотношение сторон: {rect.Size.Width / rect.Size.Height:F2}");
                }
            }

            Console.WriteLine($"\n✅ Найдено прямоугольников: {rectangles.Count}");

            // Рисуем результат
            if (rectangles.Count > 0)
            {
                using var result = image.Clone();

                for (int i = 0; i < rectangles.Count; i++)
                {
                    var rect = rectangles[i];
                    var color = i == 0 ? Scalar.Red : Scalar.Green;

                    // Рисуем rotated rectangle
                    var points = Cv2.BoxPoints(rect);
                    for (int j = 0; j < 4; j++)
                    {
                        Cv2.Line(result, (Point)points[j], (Point)points[(j + 1) % 4], color, 2);
                    }

                    // Подписываем
                    Cv2.PutText(result, $"Rect #{i + 1}",
                        (Point)rect.Center,
                        HersheyFonts.HersheySimplex, 0.7, color, 2);
                }

                var outputPath = imagePath.Replace(".jpg", "_rectangles.jpg");
                Cv2.ImWrite(outputPath, result);
                Console.WriteLine($"✅ Результат сохранён: {outputPath}");
            }
        }
    }
}