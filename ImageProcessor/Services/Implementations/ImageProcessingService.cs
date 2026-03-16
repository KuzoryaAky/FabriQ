using ImageProcessor.Models.Entities;
using ImageProcessor.Models.Requests;
using ImageProcessor.Models.Responses;
using ImageProcessor.Services.Interfaces;

namespace ImageProcessor.Services
{
    public class ImageProcessingService : IImageProcessingService
    {
        private readonly ILogger<ImageProcessingService> _logger;

        public ImageProcessingService(ILogger<ImageProcessingService> logger)
        {
            _logger = logger;
        }

        public async Task<MeasurementResponse> ProcessImageAsync(MeasurementRequest request)
        {
            string savedFilePath = null;

            try
            {
                Console.WriteLine("\n" + new string('=', 50));
                Console.WriteLine("=== НАЧАЛО ОБРАБОТКИ ===");

                if (request.Image == null || request.Image.Length == 0)
                    throw new ArgumentException("Файл не загружен или пустой");

                savedFilePath = await SaveUploadedFile(request);


                if (File.Exists(savedFilePath))
                {
                    var detectionResult = StrictRectangleDetector.FindMainRectangle(savedFilePath);
                
                    if (detectionResult.Success)
                    {
                        Console.WriteLine($"✅ Найден главный прямоугольник!");
                        Console.WriteLine($"   Углы: ");
                        for (int i = 0; i < detectionResult.Corners.Length; i++)
                        {
                            Console.WriteLine($"   [{i}] ({detectionResult.Corners[i].X}, {detectionResult.Corners[i].Y})");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"❌ Не удалось найти прямоугольник: {detectionResult.Error}");
                    }
                }


                if (File.Exists(savedFilePath))
                {
                    OpenCvExperiment.TestOpenCVWithDebug(savedFilePath);
                }

                return new MeasurementResponse
                {
                    Success = true,
                    WidthMm = 600.5,      // TODO: Заменить на реальные значения из детекции
                    HeightMm = 400.2,     // TODO: Заменить на реальные значения из детекции
                    Error = null,
                    ProcessingTime = DateTime.UtcNow,
                    FileName = request.Image.FileName,
                    FileSizeBytes = request.Image.Length,
                    Confidence = 0.95,    // TODO: Заменить на реальное значение
                    MaterialType = "stone" // TODO: Заменить на реальное значение
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Критическая ошибка обработки");

                return new MeasurementResponse
                {
                    Success = false,
                    WidthMm = 0,
                    HeightMm = 0,
                    Error = $"Ошибка обработки: {ex.Message}",
                    ProcessingTime = DateTime.UtcNow
                };
            }
            finally
            {
                _logger.LogInformation("=== ОБРАБОТКА ЗАВЕРШЕНА ===");
            }
        }

        public async Task<MeasurementResponse> ProcessImageAsync(MeasurementRecord request)
        {
            string savedFilePath = null;

            try
            {
                Console.WriteLine("\n" + new string('=', 50));
                Console.WriteLine("=== НАЧАЛО ОБРАБОТКИ ===");

                if (request.ImageData == null || request.ImageData.Length == 0)
                    throw new ArgumentException("Файл не загружен или пустой");

                savedFilePath = await SaveUploadedFile(request);

                if (File.Exists(savedFilePath))
                {
                    var detectionResult = StrictRectangleDetector.FindMainRectangle(savedFilePath);

                    if (detectionResult.Success)
                    {
                        Console.WriteLine($"✅ Найден главный прямоугольник!");
                        Console.WriteLine($"   Углы: ");
                        for (int i = 0; i < detectionResult.Corners.Length; i++)
                        {
                            Console.WriteLine($"   [{i}] ({detectionResult.Corners[i].X}, {detectionResult.Corners[i].Y})");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"❌ Не удалось найти прямоугольник: {detectionResult.Error}");
                    }
                }


                if (File.Exists(savedFilePath))
                {
                    OpenCvExperiment.TestOpenCVWithDebug(savedFilePath);
                }

                return new MeasurementResponse
                {
                    Success = true,
                    WidthMm = 600.5,  // TODO: Заменить на реальные значения из детекции
                    HeightMm = 400.2, // TODO: Заменить на реальные значения из детекции
                    Error = null,
                    ProcessingTime = DateTime.UtcNow,
                    FileSizeBytes = request.ImageData.Length,
                    Confidence = 0.95,    // TODO: Заменить на реальное значение
                    MaterialType = "stone" // TODO: Заменить на реальное значение
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Критическая ошибка обработки");

                return new MeasurementResponse
                {
                    Success = false,
                    WidthMm = 0,
                    HeightMm = 0,
                    Error = $"Ошибка обработки: {ex.Message}",
                    ProcessingTime = DateTime.UtcNow
                };
            }
            finally
            {
                _logger.LogInformation("=== ОБРАБОТКА ЗАВЕРШЕНА ===");
            }
        }

        private async Task<string> SaveUploadedFile(MeasurementRecord request)
        {
            var debugFolder = Path.Combine(Directory.GetCurrentDirectory(), "DebugUploads");
            if (!Directory.Exists(debugFolder))
                Directory.CreateDirectory(debugFolder);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var safeFileName = $"{timestamp}_{Guid.NewGuid():N}.jpg";
            var savePath = Path.Combine(debugFolder, safeFileName);

            using var memoryStream = new MemoryStream(request.ImageData);
            using var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write);
            await memoryStream.CopyToAsync(fileStream);

            Console.WriteLine($"💾 Файл сохранён: {savePath}");
            Console.WriteLine($"   Размер: {request.ImageData.Length} байт");

            return savePath;
        }

        private async Task<string> SaveUploadedFile(MeasurementRequest request)
        {
            var debugFolder = Path.Combine(Directory.GetCurrentDirectory(), "DebugUploads");
            if (!Directory.Exists(debugFolder))
                Directory.CreateDirectory(debugFolder);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var safeFileName = $"{timestamp}_{Guid.NewGuid():N}.jpg";
            var savePath = Path.Combine(debugFolder, safeFileName);

            using var memoryStream = new MemoryStream();
            await request.Image.CopyToAsync(memoryStream);
            var fileBytes = memoryStream.ToArray();

            await System.IO.File.WriteAllBytesAsync(savePath, fileBytes);

            Console.WriteLine($"💾 Файл сохранён: {savePath}");
            Console.WriteLine($"   Размер: {fileBytes.Length} байт");

            return savePath;
        }
    }
}