using ImageProcessor.Infrastructure.Data;
using ImageProcessor.Models.Requests;
using ImageProcessor.Models.Responses;
using ImageProcessor.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

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
    } 
}
