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
        private readonly ILogger<MeasurementController> _logger;
        private readonly AppDbContext _context;

        public MeasurementController(IImageProcessingService processingService, ILogger<MeasurementController> logger, AppDbContext context)
        {
            _context = context;
            _processingService = processingService;
            _logger = logger;
        }

        //// Простой метод для проверки, что API работает
        [HttpPost("testDetecter")]
        public async Task<IActionResult> Test([FromForm] MeasurementRequest response)
        {
            var test = await _processingService.ProcessImageAsync(response);

            return Ok("Детектер отработал, проверяй");
        }


        /// <summary>
        /// Метод для детекции и загрузки её в БД
        /// </summary>
        /// <param name="request">принимает фотографию которой нужна детекция</param>
        /// <returns>возращает на телефон id фотографии</returns>
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

                MeasurementRequest data = new()
                {
                    Image = request.Image
                };


                _ = Task.Run(() => _processingService.ProcessImageAsync(data));

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

        /// <summary>
        /// Возщает фотографию с детекцией(на которой уже нашлись контуры и опредилился размер материала)
        /// </summary>
        /// <param name="id">id фотографии которой нужно получить из базы</param>
        /// <returns></returns>
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
