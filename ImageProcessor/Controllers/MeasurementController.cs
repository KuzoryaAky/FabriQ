using ImageProcessor.Infrastructure.Data;
using ImageProcessor.Models.Entities;
using ImageProcessor.Models.Requests;
using ImageProcessor.Models.Responses;
using ImageProcessor.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ImageProcessor.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MeasurementController : ControllerBase
    {
        private readonly IImageProcessingService _processingService;
        private static long _counter = 0;
        private readonly ILogger<MeasurementController> _logger;
        private readonly AppDbContext _context;

        public MeasurementController(IImageProcessingService processingService, ILogger<MeasurementController> logger, AppDbContext context)
        {
            _context = context;
            _processingService = processingService;
            _logger = logger;
        }

        // Этот метод будет вызываться при POST запросе на /api/Measurement
        [HttpPost("measure")]
        [ProducesResponseType(typeof(MeasurementResponse), 200)]
        [ProducesResponseType(typeof(MeasurementResponse), 400)]
        public async Task<IActionResult> Measure([FromForm] MeasurementRequest request)
        {
            try
            {
                // Передаём запрос в сервис для обработки
                var response = await _processingService.ProcessImageAsync(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                // Если что-то пошло не так
                var errorResponse = new MeasurementResponse
                {
                    Success = false,
                    WidthMm = 0,
                    HeightMm = 0,
                    Error = $"Ошибка обработки: {ex.Message}"
                };
                return BadRequest(errorResponse);
            }
        }

        // Простой метод для проверки, что API работает
        [HttpGet("test")]
        public IActionResult Test()
        {
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();
            var count = Interlocked.Increment(ref _counter);

            Console.WriteLine($"Запрос от IP: {clientIp}");
            Console.WriteLine($"User-Agent: {userAgent}");
            Console.WriteLine($"Запрос #{count}");
            Console.WriteLine("-----------------------------------------------------");
            return Ok("API работает");
        }

        // Метод для загрузки фото в БД и возращает на телефон id фотографии
        [HttpPut("putImage")]
        [ProducesResponseType(typeof(MeasurementResponse), 200)]
        [ProducesResponseType(typeof(MeasurementResponse), 400)]
        public async Task<IActionResult> PutImageToDbAndReturnID([FromForm] MeasurementRequest request)
        {
            try
            {
                if (request?.Image == null)
                    return BadRequest(new MeasurementResponse
                    {
                        Success = false,
                    });

                string fileName = request.Image.FileName;
                byte[] imageBytes;
                using (var memoryStream = new MemoryStream())
                {
                    await request.Image.CopyToAsync(memoryStream);
                    imageBytes = memoryStream.ToArray();
                }

                MeasurementRecord putImage = new()
                {
                    FileName = fileName,
                    ImageData = imageBytes,
                    ProcessedDate = DateTime.UtcNow
                };

                await _context.MeasurementRecords.AddAsync(putImage);

                await _context.SaveChangesAsync();

                return Ok(putImage.Id);
            }
            catch (Exception ex)
            {
                return BadRequest(new MeasurementResponse
                {
                    Success = false,
                });
            }
        }

        [HttpGet("getDetectImage")]
        [ProducesResponseType(typeof(MeasurementResponse), 200)]
        [ProducesResponseType(typeof(MeasurementResponse), 400)]
        public async Task<IActionResult> GetDetectImage([FromQuery]int id)
        {
            try
            {
                if (id is 0)
                    return BadRequest(new MeasurementResponse
                    {
                        Success = false,
                        Error = $"Неверный ID фотографии"
                    });

                MeasurementRecord imageData = await _context.MeasurementRecords.Where(data => data.Id == id).FirstAsync();

                if (imageData is null)
                    return BadRequest(new MeasurementResponse
                    {
                        Success = false,
                        Error = $"Не найден ID фотографии"
                    });

                return Ok(imageData);
            }
            catch (Exception ex)
            {
                var errorResponse = new MeasurementResponse
                {
                    Success = false,
                    WidthMm = 0,
                    HeightMm = 0,
                    Error = $"Ошибка обработки: {ex.Message}"
                };
                return BadRequest(errorResponse);
            }
        }
    } 
}
