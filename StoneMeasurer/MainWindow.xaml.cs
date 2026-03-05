using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using System.Collections.Generic;

namespace StoneMeasurer
{
    public partial class MainWindow : Window
    {
        private BitmapImage _currentImage;
        private string _currentImagePath;

        private List<Point> _polygonPoints = new List<Point>();
        private bool _isDrawingMode = false;
        private Polygon _selectionPolygon = null;
        private List<System.Windows.Shapes.Ellipse> _pointVisuals = new List<System.Windows.Shapes.Ellipse>();

        private bool _isDraggingPoint = false;
        private int _draggedPointIndex = -1;
        private Point _dragStartPoint;

        private List<Point> _etalonPoints = new List<Point>();
        private bool _isEtalonMode = false;
        private double _scale = 0; // мм на пиксель
        private bool _isCalibrated = false;

        private double _baseScale = 0; // Базовый масштаб из калибровки
        private double _scaleFactor = 1.0; // Множитель корректировки

        public MainWindow()
        {
            InitializeComponent();
            UpdateButtonsState();
        }

        // Обработчик для кнопки загрузки фото
        private void LoadStoneButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Выберите фото камня",
                Filter = "Изображения|*.jpg;*.jpeg;*.png;*.bmp;*.gif|Все файлы|*.*",
                Multiselect = false,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    LoadImage(openFileDialog.FileName);
                    _currentImagePath = openFileDialog.FileName;

                    // Обновляем информацию о загруженном файле
                    MeasurementInfoText.Text = $"Загружено: {System.IO.Path.GetFileName(_currentImagePath)}";

                    // Активируем кнопки
                    UpdateButtonsState();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при загрузке изображения: {ex.Message}",
                                   "Ошибка",
                                   MessageBoxButton.OK,
                                   MessageBoxImage.Error);
                }
            }
        }

        // Обработчик переключения режимов
        private void Mode_Changed(object sender, RoutedEventArgs e)
        {
            if (ModeStone.IsChecked == true)
            {
                _isEtalonMode = false;
                EtalonPanel.Visibility = Visibility.Collapsed;
                ModeInfoText.Text = "Режим: измерение камня";

                // Переключаем отображение на выделение камня
                SwitchToStoneMode();
            }
            else
            {
                _isEtalonMode = true;
                EtalonPanel.Visibility = Visibility.Visible;
                ModeInfoText.Text = "Режим: калибровка эталона";

                // Переключаем на выделение эталона
                SwitchToEtalonMode();
            }
        }

        // Переключение на режим камня
        private void SwitchToStoneMode()
        {
            // Показываем выделение камня, скрываем эталон
            // (если нужно визуально различать)
            if (_selectionPolygon != null)
            {
                _selectionPolygon.Stroke = Brushes.Blue;
                _selectionPolygon.Fill = new SolidColorBrush(Color.FromArgb(40, 0, 120, 255));
            }
        }

        // Переключение на режим эталона
        private void SwitchToEtalonMode()
        {
            // Показываем выделение эталона другим цветом
            if (_selectionPolygon != null)
            {
                _selectionPolygon.Stroke = Brushes.Green;
                _selectionPolygon.Fill = new SolidColorBrush(Color.FromArgb(40, 0, 255, 0));
            }
        }



        private void StoneImage_Loaded(object sender, RoutedEventArgs e)
        {
            // Очищаем выделение при загрузке нового изображения
            ClearSelection();
        }

        // Метод для загрузки изображения
        private void LoadImage(string filePath)
        {
            _currentImage = new BitmapImage();
            _currentImage.BeginInit();
            _currentImage.CacheOption = BitmapCacheOption.OnLoad;
            _currentImage.UriSource = new Uri(filePath);
            _currentImage.EndInit();

            StoneImage.Source = _currentImage;
            StoneImage.Visibility = Visibility.Visible;

            // Показываем Canvas (но не включаем режим рисования)
            DrawingCanvas.Visibility = Visibility.Visible;

            // Скрываем плейсхолдер
            var parentGrid = StoneImage.Parent as Grid;
            if (parentGrid != null)
            {
                foreach (var child in parentGrid.Children)
                {
                    if (child != StoneImage && child is TextBlock)
                    {
                        ((TextBlock)child).Visibility = Visibility.Collapsed;
                    }
                }
            }

            // Очищаем предыдущее выделение
            ClearSelection();
        }

        

        private double CalculatePolygonPerimeter(List<Point> points)
        {
            double perimeter = 0;
            int count = points.Count;

            for (int i = 0; i < count; i++)
            {
                Point p1 = points[i];
                Point p2 = points[(i + 1) % count];
                perimeter += Distance(p1, p2);
            }

            return perimeter;
        }

        private double Distance(Point p1, Point p2)
        {
            double dx = p1.X - p2.X;
            double dy = p1.Y - p2.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private double CalculatePolygonArea(List<Point> points)
        {
            double area = 0;
            int count = points.Count;

            for (int i = 0; i < count; i++)
            {
                Point p1 = points[i];
                Point p2 = points[(i + 1) % count];
                area += p1.X * p2.Y - p2.X * p1.Y;
            }

            return Math.Abs(area) / 2.0;
        }

        // Временный метод измерения (для демонстрации)
        private void MeasureStone()
        {
            // Имитация процесса измерения
            var random = new Random();
            double length = Math.Round(50 + random.NextDouble() * 100, 1);  // 50-150 мм
            double width = Math.Round(30 + random.NextDouble() * 70, 1);    // 30-100 мм
            double height = Math.Round(20 + random.NextDouble() * 50, 1);   // 20-70 мм
            double weight = Math.Round((length * width * height) / 1000 * 2.5, 1); // Примерный вес

            // Обновляем информацию
            MeasurementInfoText.Text = $"📏 Длина: {length} мм | Ширина: {width} мм | Высота: {height} мм | Вес: ~{weight} г";

            // Активируем кнопку экспорта
            ExportButton.IsEnabled = true;

            // Показываем сообщение
            MessageBox.Show($"Измерение завершено!\n\n" +
                          $"Длина: {length} мм\n" +
                          $"Ширина: {width} мм\n" +
                          $"Высота: {height} мм\n" +
                          $"Примерный вес: {weight} г",
                          "Результаты измерения",
                          MessageBoxButton.OK,
                          MessageBoxImage.Information);
        }

        // Обработчик для кнопки экспорта
        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentImage == null)
            {
                MessageBox.Show("Нет данных для экспорта!",
                               "Предупреждение",
                               MessageBoxButton.OK,
                               MessageBoxImage.Warning);
                return;
            }

            try
            {
                ExportResults();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при экспорте: {ex.Message}",
                               "Ошибка",
                               MessageBoxButton.OK,
                               MessageBoxImage.Error);
            }
        }

        // Метод экспорта результатов
        private void ExportResults()
        {
            var saveFileDialog = new SaveFileDialog
            {
                Title = "Сохранить результаты измерения",
                Filter = "Текстовый файл|*.txt|CSV файл|*.csv|Все файлы|*.*",
                DefaultExt = "txt",
                FileName = $"StoneMeasurement_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                // Формируем данные для сохранения
                string data = $"Результаты измерения камня\n" +
                             $"==========================\n" +
                             $"Дата: {DateTime.Now:dd.MM.yyyy HH:mm:ss}\n" +
                             $"Файл: {_currentImagePath ?? "Неизвестно"}\n" +
                             $"{MeasurementInfoText.Text}\n" +
                             $"\nПримечание: результаты являются предварительными.";

                // Сохраняем файл
                File.WriteAllText(saveFileDialog.FileName, data);

                MessageBox.Show($"Результаты успешно сохранены в:\n{saveFileDialog.FileName}",
                               "Экспорт завершен",
                               MessageBoxButton.OK,
                               MessageBoxImage.Information);
            }
        }

        // Метод для обновления состояния кнопок
        private void UpdateButtonsState()
        {
            bool hasImage = _currentImage != null;
            MeasureButton.IsEnabled = hasImage;
            // ExportButton активируется после измерения
        }

        // Дополнительный метод: очистка фото
        public void ClearImage()
        {
            _currentImage = null;
            _currentImagePath = null;
            StoneImage.Source = null;
            StoneImage.Visibility = Visibility.Collapsed;

            // Показываем плейсхолдер
            var parentGrid = StoneImage.Parent as Grid;
            if (parentGrid != null)
            {
                foreach (var child in parentGrid.Children)
                {
                    if (child != StoneImage && child is TextBlock)
                    {
                        ((TextBlock)child).Visibility = Visibility.Visible;
                    }
                }
            }

            MeasurementInfoText.Text = "Результаты измерений будут здесь";
            MeasureButton.IsEnabled = false;
            ExportButton.IsEnabled = false;
        }

        // Обработчик закрытия окна
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_currentImage != null)
            {
                var result = MessageBox.Show("Закрыть приложение?",
                                            "Подтверждение",
                                            MessageBoxButton.YesNo,
                                            MessageBoxImage.Question);
                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                }
            }
            base.OnClosing(e);
        }

        // Обработчики кнопок выделения
        private void StartSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            if (StoneImage.Source == null)
            {
                MessageBox.Show("Сначала загрузите фото!", "Предупреждение",
                               MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _isDrawingMode = true;

            if (_isEtalonMode)
            {
                _etalonPoints.Clear();
                ModeInfoText.Text = "Режим калибровки: выделите эталон";
            }
            else
            {
                _polygonPoints.Clear();
                ModeInfoText.Text = "Режим измерения: выделите камень";
            }

            ClearDrawingCanvas();

            StartSelectionButton.IsEnabled = false;
            ClearSelectionButton.IsEnabled = true;
            CompleteSelectionButton.IsEnabled = true;

            DrawingCanvas.Visibility = Visibility.Visible;
            DrawingCanvas.Cursor = Cursors.Cross;

            SelectionInfoText.Text = "Точек: 0";
        }

        private void ClearSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            ClearSelection();
        }

        // Метод для отображения размеров между точками в миллиметрах
        private void DrawMeasurements()
        {
            // Удаляем старые надписи с размерами
            var oldTexts = DrawingCanvas.Children.OfType<Border>().Where(b => b.Tag?.ToString() == "MeasurementBorder").ToList();
            foreach (var text in oldTexts)
            {
                DrawingCanvas.Children.Remove(text);
            }

            var currentPoints = _isEtalonMode ? _etalonPoints : _polygonPoints;

            if (currentPoints.Count < 2) return;

            // Проверяем, есть ли калибровка
            if (!_isCalibrated || _scale <= 0)
            {
                // Если нет калибровки, показываем сообщение
                TextBlock noCalibText = new TextBlock
                {
                    Text = "❗ Выполните калибровку",
                    Foreground = Brushes.Red,
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Background = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                    Padding = new Thickness(5)
                };

                Border noCalibBorder = new Border
                {
                    Child = noCalibText,
                    BorderBrush = Brushes.Red,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(3),
                    Tag = "MeasurementBorder"
                };

                Canvas.SetLeft(noCalibBorder, 10);
                Canvas.SetTop(noCalibBorder, 10);
                DrawingCanvas.Children.Add(noCalibBorder);
                return;
            }

            // Для каждой пары соседних точек
            for (int i = 0; i < currentPoints.Count; i++)
            {
                int j = (i + 1) % currentPoints.Count; // Следующая точка (замыкаем круг)

                Point p1 = currentPoints[i];
                Point p2 = currentPoints[j];

                // Вычисляем расстояние в пикселях
                double distancePixels = Distance(p1, p2);

                // Переводим в миллиметры
                double distanceMm = distancePixels * _scale;

                // Формируем текст с размером
                string distanceText = $"{distanceMm:F1} мм";

                // Находим середину отрезка
                Point midPoint = new Point((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2);

                // Создаем текстовый блок с размером
                TextBlock measurementText = new TextBlock
                {
                    Text = distanceText,
                    Foreground = Brushes.Black,
                    Background = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Padding = new Thickness(4, 2, 4, 2)
                };

                // Добавляем небольшую рамку
                Border border = new Border
                {
                    Child = measurementText,
                    BorderBrush = new SolidColorBrush(Color.FromArgb(150, 0, 0, 255)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(3),
                    Background = Brushes.Transparent,
                    Tag = "MeasurementBorder"
                };

                // Вычисляем примерную ширину текста для центрирования
                double textWidth = distanceText.Length * 6; // Приблизительно 6 пикс на символ
                Canvas.SetLeft(border, midPoint.X - textWidth / 2);
                Canvas.SetTop(border, midPoint.Y - 10);

                // Добавляем на Canvas
                DrawingCanvas.Children.Add(border);
            }

            // Добавляем информацию о масштабе
            TextBlock scaleInfo = new TextBlock
            {
                Text = $"Масштаб: 1 пикс = {_scale:F3} мм",
                Foreground = Brushes.DarkBlue,
                FontSize = 10,
                Background = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                Padding = new Thickness(3)
            };

            Border scaleBorder = new Border
            {
                Child = scaleInfo,
                BorderBrush = Brushes.Blue,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Tag = "MeasurementBorder"
            };

            Canvas.SetLeft(scaleBorder, 10);
            Canvas.SetTop(scaleBorder, 10);
            DrawingCanvas.Children.Add(scaleBorder);
        }


        // Вычисление максимального размера многоугольника
        private double CalculatePolygonMaxDimension(List<Point> points)
        {
            double maxDist = 0;

            for (int i = 0; i < points.Count; i++)
            {
                for (int j = i + 1; j < points.Count; j++)
                {
                    double dist = Distance(points[i], points[j]);
                    if (dist > maxDist)
                        maxDist = dist;
                }
            }

            return maxDist;
        }

        // Вычисление длины линии (для режима линейки)
        private double CalculateLineLength()
        {
            if (_etalonPoints.Count < 2)
                return 0;

            // Ищем две самые удаленные точки (предполагаем, что это концы линейки)
            return CalculatePolygonMaxDimension(_etalonPoints);
        }

        // Обновленный метод измерения (теперь использует масштаб)
        private void MeasureButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentImage == null)
            {
                MessageBox.Show("Сначала загрузите фото камня!",
                               "Предупреждение",
                               MessageBoxButton.OK,
                               MessageBoxImage.Warning);
                return;
            }

            if (_polygonPoints.Count < 3)
            {
                MessageBox.Show("Сначала выделите область измерения!",
                               "Предупреждение",
                               MessageBoxButton.OK,
                               MessageBoxImage.Warning);
                return;
            }

            if (!_isCalibrated)
            {
                MessageBox.Show("Сначала выполните калибровку по эталону!",
                               "Предупреждение",
                               MessageBoxButton.OK,
                               MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Вычисляем все размеры между точками в мм
                string allDimensions = "";
                for (int i = 0; i < _polygonPoints.Count; i++)
                {
                    int j = (i + 1) % _polygonPoints.Count;
                    double distMm = Distance(_polygonPoints[i], _polygonPoints[j]) * _scale;
                    allDimensions += $"Сторона {i + 1}-{j + 1}: {distMm:F1} мм\n";
                }

                // Вычисляем площадь и периметр
                double areaPixels = CalculatePolygonArea(_polygonPoints);
                double perimeterPixels = CalculatePolygonPerimeter(_polygonPoints);

                double areaMm = areaPixels * _scale * _scale;
                double perimeterMm = perimeterPixels * _scale;

                // Находим минимальный и максимальный размеры
                double maxDimension = CalculatePolygonMaxDimension(_polygonPoints) * _scale;

                MeasurementInfoText.Text = $"📐 Площадь: {areaMm:F1} мм² | Периметр: {perimeterMm:F1} мм";
                SelectionInfoText.Text = $"📏 Макс. размер: {maxDimension:F1} мм";

                // Активируем кнопку экспорта
                ExportButton.IsEnabled = true;

                // Показываем детальные результаты
                MessageBox.Show($"РЕЗУЛЬТАТЫ ИЗМЕРЕНИЯ\n\n" +
                              $"Все размеры сторон:\n{allDimensions}\n" +
                              $"Площадь: {areaMm:F1} мм²\n" +
                              $"Периметр: {perimeterMm:F1} мм\n" +
                              $"Максимальный размер: {maxDimension:F1} мм\n\n" +
                              $"Масштаб: 1 пикс = {_scale:F3} мм",
                              "Результаты измерения",
                              MessageBoxButton.OK,
                              MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при измерении: {ex.Message}",
                               "Ошибка",
                               MessageBoxButton.OK,
                               MessageBoxImage.Error);
            }
        }

        // Применение калибровки по эталону
        private void ApplyEtalonButton_Click(object sender, RoutedEventArgs e)
        {
            // Проверяем, что выделено достаточно точек (минимум 4 для прямоугольника)
            if (_etalonPoints.Count < 4)
            {
                MessageBox.Show("Для калибровки нужно выделить 4 угловые точки прямоугольника!",
                               "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Считываем размеры из полей ввода
            if (!double.TryParse(EtalonWidthInput.Text, out double realWidth) || realWidth <= 0 ||
                !double.TryParse(EtalonHeightInput.Text, out double realHeight) || realHeight <= 0)
            {
                MessageBox.Show("Введите корректные ширину и высоту прямоугольника (положительные числа)!",
                               "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Вычисляем размер прямоугольника в пикселях
                double pixelWidth = CalculatePolygonWidth(_etalonPoints);
                double pixelHeight = CalculatePolygonHeight(_etalonPoints);

                if (pixelWidth <= 0 || pixelHeight <= 0)
                {
                    MessageBox.Show("Не удалось определить размеры прямоугольника. Проверьте правильность выделения.",
                                   "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Вычисляем масштаб по горизонтали и вертикали
                double scaleX = realWidth / pixelWidth;
                double scaleY = realHeight / pixelHeight;

                // Используем средний масштаб (или можно сохранять оба для непрямоугольных объектов)
                _scale = (scaleX + scaleY) / 2;
                _isCalibrated = true;

                // Показываем информацию о масштабе
                ScaleInfoText.Text = $"Масштаб: 1 пикс = {_scale:F4} мм\n" +
                                    $"Горизонталь: {scaleX:F4} мм/пикс, Вертикаль: {scaleY:F4} мм/пикс";
                ScaleInfoText.Visibility = Visibility.Visible;

                ModeInfoText.Text = "Калибровка выполнена! Можно переключаться на измерение камня.";

                // Переключаем на режим камня
                ModeStone.IsChecked = true;

                MessageBox.Show($"КАЛИБРОВКА ВЫПОЛНЕНА\n\n" +
                               $"Размер эталона в пикселях:\n" +
                               $"• Ширина: {pixelWidth:F1} пикс\n" +
                               $"• Высота: {pixelHeight:F1} пикс\n\n" +
                               $"Реальный размер:\n" +
                               $"• Ширина: {realWidth} мм\n" +
                               $"• Высота: {realHeight} мм\n\n" +
                               $"Масштаб:\n" +
                               $"• 1 пиксель = {_scale:F4} мм\n" +
                               $"• 1 мм = {1 / _scale:F2} пикс",
                               "Калибровка", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при калибровке: {ex.Message}",
                               "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // Сохраняем базовый масштаб
            _baseScale = _scale;
            _scaleFactor = 1.0;

            // Показываем панель корректировки
            ScaleAdjustPanel.Visibility = Visibility.Visible;
            ScaleSlider.Value = 1.0;

            // Обновляем отображение
            UpdateScaleDisplay();
        }

        private void ScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _scaleFactor = e.NewValue;
            _scale = _baseScale * _scaleFactor;

            // Обновляем текст множителя
            ScaleFactorText.Text = $"{_scaleFactor:F2}x";

            // Обновляем информацию о масштабе
            ScaleInfoText.Text = $"Масштаб: 1 пикс = {_scale:F4} мм (корр: {_scaleFactor:F2})";

            // Перерисовываем все размеры с новым масштабом
            if (_polygonPoints.Count > 0 || _etalonPoints.Count > 0)
            {
                DrawPolygon(); // Это вызовет DrawMeasurements
            }
        }

        private void UpdateScaleDisplay()
        {
            ScaleInfoText.Text = $"Масштаб: 1 пикс = {_scale:F4} мм";

            // Если есть выделение, обновляем размеры
            if (_polygonPoints.Count > 0 || _etalonPoints.Count > 0)
            {
                DrawPolygon();
            }
        }

        private double CalculatePolygonWidth(List<Point> points)
        {
            if (points.Count < 2) return 0;

            double minX = points.Min(p => p.X);
            double maxX = points.Max(p => p.X);

            return maxX - minX;
        }

        private double CalculatePolygonHeight(List<Point> points)
        {
            if (points.Count < 2) return 0;

            double minY = points.Min(p => p.Y);
            double maxY = points.Max(p => p.Y);

            return maxY - minY;
        }

        // Вспомогательный метод для получения ограничивающего прямоугольника
        private Rect GetBoundingRect(List<Point> points)
        {
            if (points.Count == 0) return Rect.Empty;

            double minX = points.Min(p => p.X);
            double minY = points.Min(p => p.Y);
            double maxX = points.Max(p => p.X);
            double maxY = points.Max(p => p.Y);

            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        private void CompleteSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            var currentPoints = _isEtalonMode ? _etalonPoints : _polygonPoints;

            // Для эталона нужны минимум 4 точки (прямоугольник)
            int minPoints = _isEtalonMode ? 4 : 3;

            if (currentPoints.Count < minPoints)
            {
                MessageBox.Show(_isEtalonMode ?
                               "Для прямоугольника нужно выделить 4 угловые точки!" :
                               "Нужно минимум 3 точки для выделения области!",
                               "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _isDrawingMode = false;
            _isDraggingPoint = false;
            DrawingCanvas.Cursor = Cursors.Arrow;

            // Снимаем выделение со всех точек
            for (int i = 0; i < _pointVisuals.Count; i++)
            {
                HighlightPoint(i, false);
            }

            StartSelectionButton.IsEnabled = true;
            ClearSelectionButton.IsEnabled = true;
            CompleteSelectionButton.IsEnabled = false;

            if (_isEtalonMode)
            {
                ModeInfoText.Text = "Прямоугольник выделен. Введите размеры и нажмите 'Применить калибровку'";

                // Показываем размеры в пикселях для информации
                double width = CalculatePolygonWidth(_etalonPoints);
                double height = CalculatePolygonHeight(_etalonPoints);
                SelectionInfoText.Text = $"Размер в пикселях: {width:F1} x {height:F1}";
            }
            else
            {
                ModeInfoText.Text = "Камень выделен. Нажмите 'Измерить'";
                MeasureButton.IsEnabled = _isCalibrated;
            }
        }

        private void ClearSelection()
        {
            _isDrawingMode = false;
            _polygonPoints.Clear();
            _etalonPoints.Clear();
            ClearDrawingCanvas();

            StartSelectionButton.IsEnabled = true;
            ClearSelectionButton.IsEnabled = false;
            CompleteSelectionButton.IsEnabled = false;

            DrawingCanvas.Visibility = Visibility.Collapsed;
            DrawingCanvas.Cursor = Cursors.Arrow;

            if (_isEtalonMode)
            {
                ModeInfoText.Text = "Режим: калибровка эталона";
                _isCalibrated = false;
                ScaleInfoText.Visibility = Visibility.Collapsed;
            }
            else
            {
                ModeInfoText.Text = "Режим: измерение камня";
            }

            MeasurementInfoText.Text = "Результаты измерений будут здесь";
            SelectionInfoText.Text = "";
            MeasureButton.IsEnabled = false;
        }

        private void ClearDrawingCanvas()
        {
            DrawingCanvas.Children.Clear();
            _selectionPolygon = null;
            _pointVisuals.Clear();
            _isDraggingPoint = false;
            _draggedPointIndex = -1;
        }

        private void DrawingCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isDrawingMode) return;

            var clickedPoint = e.GetPosition(DrawingCanvas);
            int pointIndex = FindPointAtPosition(clickedPoint);

            if (pointIndex >= 0)
            {
                // Начинаем перемещение существующей точки
                _isDraggingPoint = true;
                _draggedPointIndex = pointIndex;
                _dragStartPoint = clickedPoint;

                HighlightPoint(pointIndex, true);
                DrawingCanvas.CaptureMouse();
                e.Handled = true;
            }
            else
            {
                // Добавляем новую точку в соответствующий список
                if (_isEtalonMode)
                {
                    _etalonPoints.Add(clickedPoint);
                }
                else
                {
                    _polygonPoints.Add(clickedPoint);
                }

                // Рисуем точку
                DrawPoint(clickedPoint, _isEtalonMode ?
                          _etalonPoints.Count - 1 : _polygonPoints.Count - 1);

                // Обновляем полигон
                DrawPolygon();

                int totalPoints = _isEtalonMode ? _etalonPoints.Count : _polygonPoints.Count;
                SelectionInfoText.Text = $"Точек: {totalPoints}";
            }
        }

        private void HighlightPoint(int index, bool highlight)
        {
            if (index >= 0 && index < _pointVisuals.Count)
            {
                var point = _pointVisuals[index];
                if (highlight)
                {
                    point.Width = 14;
                    point.Height = 14;
                    point.Fill = new SolidColorBrush(Color.FromArgb(100, 255, 255, 0));
                    point.Stroke = Brushes.Red;
                    point.StrokeThickness = 2;
                }
                else
                {
                    point.Width = 10;
                    point.Height = 10;
                    point.Fill = Brushes.White;
                    point.Stroke = Brushes.Blue;
                    point.StrokeThickness = 2;
                }
            }
        }

        private int FindPointAtPosition(Point position, double tolerance = 15)
        {
            for (int i = 0; i < _pointVisuals.Count; i++)
            {
                var point = _pointVisuals[i];
                double left = Canvas.GetLeft(point);
                double top = Canvas.GetTop(point);
                double centerX = left + point.Width / 2;
                double centerY = top + point.Height / 2;

                double distance = Math.Sqrt(Math.Pow(position.X - centerX, 2) +
                                            Math.Pow(position.Y - centerY, 2));

                if (distance <= tolerance)
                {
                    return i;
                }
            }
            return -1;
        }
        private void DrawingCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_isDraggingPoint)
            {
                // Если мышь ушла за пределы Canvas при перемещении
                _isDraggingPoint = false;

                if (_draggedPointIndex >= 0)
                    HighlightPoint(_draggedPointIndex, false);

                _draggedPointIndex = -1;
                DrawingCanvas.ReleaseMouseCapture();
            }
        }

        private void DrawingCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingPoint)
            {
                // Завершаем перемещение
                _isDraggingPoint = false;

                // Снимаем визуальное выделение
                if (_draggedPointIndex >= 0)
                {
                    HighlightPoint(_draggedPointIndex, false);
                }

                _draggedPointIndex = -1;

                // Освобождаем захват мыши
                DrawingCanvas.ReleaseMouseCapture();

                e.Handled = true;
            }
        }

        private void DrawingCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDrawingMode) return;

            var currentPoint = e.GetPosition(DrawingCanvas);

            if (_isDraggingPoint && _draggedPointIndex >= 0)
            {
                // Перемещаем точку в соответствующем списке
                if (_isEtalonMode && _draggedPointIndex < _etalonPoints.Count)
                {
                    _etalonPoints[_draggedPointIndex] = currentPoint;
                }
                else if (!_isEtalonMode && _draggedPointIndex < _polygonPoints.Count)
                {
                    _polygonPoints[_draggedPointIndex] = currentPoint;
                }

                // Обновляем позицию визуальной точки
                if (_draggedPointIndex < _pointVisuals.Count && _pointVisuals[_draggedPointIndex] != null)
                {
                    var point = _pointVisuals[_draggedPointIndex];
                    Canvas.SetLeft(point, currentPoint.X - point.Width / 2);
                    Canvas.SetTop(point, currentPoint.Y - point.Height / 2);
                }

                // Обновляем полигон и размеры
                DrawPolygon();

                e.Handled = true;
            }
        }

        private void DrawPoint(Point position, int index)
        {
            // Убеждаемся, что у нас достаточно места в списке визуальных точек
            while (_pointVisuals.Count <= index)
            {
                _pointVisuals.Add(null);
            }

            var ellipse = new System.Windows.Shapes.Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = Brushes.White,
                Stroke = Brushes.Blue,
                StrokeThickness = 2,
                Tag = index
            };

            // Добавляем обработчики событий для точки
            ellipse.MouseEnter += (s, e) =>
            {
                if (!_isDraggingPoint && s is System.Windows.Shapes.Ellipse el)
                {
                    el.Width = 12;
                    el.Height = 12;
                    el.Stroke = Brushes.Orange;
                }
            };

            ellipse.MouseLeave += (s, e) =>
            {
                if (!_isDraggingPoint && s is System.Windows.Shapes.Ellipse el &&
                    (!_isDraggingPoint || _draggedPointIndex != (int)el.Tag))
                {
                    el.Width = 10;
                    el.Height = 10;
                    el.Stroke = Brushes.Blue;
                    el.Fill = Brushes.White;
                }
            };

            Canvas.SetLeft(ellipse, position.X - 5);
            Canvas.SetTop(ellipse, position.Y - 5);

            DrawingCanvas.Children.Add(ellipse);
            _pointVisuals[index] = ellipse;
        }

        private void DrawPolygon()
        {
            // Удаляем старый полигон
            if (_selectionPolygon != null)
                DrawingCanvas.Children.Remove(_selectionPolygon);

            var currentPoints = _isEtalonMode ? _etalonPoints : _polygonPoints;

            if (currentPoints.Count < 2) return;

            // Создаем новый полигон
            _selectionPolygon = new Polygon
            {
                Stroke = _isEtalonMode ? Brushes.Green : Brushes.Blue,
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                Fill = _isEtalonMode ?
                       new SolidColorBrush(Color.FromArgb(40, 0, 255, 0)) :
                       new SolidColorBrush(Color.FromArgb(40, 0, 120, 255))
            };

            var pointCollection = new System.Windows.Media.PointCollection();
            foreach (var point in currentPoints)
                pointCollection.Add(point);

            // Замыкаем полигон если точек больше 2
            if (currentPoints.Count >= 3)
            {
                pointCollection.Add(currentPoints[0]);
            }

            _selectionPolygon.Points = pointCollection;
            DrawingCanvas.Children.Add(_selectionPolygon);

            // Добавляем отображение размеров между точками
            DrawMeasurements();
        }
    }
}