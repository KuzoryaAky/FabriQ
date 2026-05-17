using System.Text.Json;
using FabriQ.Models.DTOs;
using ImageProcessor.Infrastructure.Data;
using ImageProcessor.Models.DTOs;
using ImageProcessor.Models.Entities;
using ImageProcessor.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
public class MeasurementController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IBackgroundTaskQueue _taskQueue; // простая очередь в памяти

    public MeasurementController(AppDbContext dbContext, IBackgroundTaskQueue taskQueue)
    {
        _dbContext = dbContext;
        _taskQueue = taskQueue;
    }

    // 1. Принять фото → сразу вернуть ID
    [HttpPost("process-image")]
    public async Task<ActionResult<ProcessResponse>> ProcessImage(IFormFile file)
    {
        // Валидация
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "File is empty" });
        if (file.Length > 10 * 1024 * 1024)
            return BadRequest(new { error = "File too large (max 10 MB)" });

        var allowedTypes = new[] { "image/jpeg", "image/png" };
        if (!allowedTypes.Contains(file.ContentType))
            return BadRequest(new { error = "Only JPEG/PNG allowed" });

        // Конвертируем файл в byte[]
        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);
        var imageData = memoryStream.ToArray();

        // Создаём запись в БД (ваша модель + новые поля)
        var record = new MeasurementRecord
        {
            FileName = file.FileName,
            ImageData = imageData,
            Status = "pending",
            CreatedAt = DateTime.UtcNow,
            RequestGuid = Guid.NewGuid()
        };

        _dbContext.MeasurementRecords.Add(record);
        await _dbContext.SaveChangesAsync();

        // Отправляем в фоновую очередь
        _taskQueue.QueueBackgroundWorkItem(async token =>
        {
            await ProcessImageAsync(record.Id, token);
        });

        return Accepted(new ProcessResponse
        {
            RequestId = record.RequestGuid,
            Status = "pending",
            Message = "Use GET /status/{id} to check progress"
        });
    }

    // 2. Проверить статус
    [HttpGet("status/{requestId:guid}")]
    public async Task<ActionResult<StatusResponse>> GetStatus(Guid requestId)
    {
        var record = await _dbContext.MeasurementRecords
            .FirstOrDefaultAsync(r => r.RequestGuid == requestId);

        if (record == null)
            return NotFound(new { error = "Request not found" });

        return new StatusResponse
        {
            RequestId = requestId,
            Status = record.Status,
            CreatedAt = record.CreatedAt,
            CompletedAt = record.CompletedAt,
            ErrorMessage = record.ErrorMessage
        };
    }

    // 3. Получить результат
    [HttpGet("result/{requestId:guid}")]
    public async Task<ActionResult<ResultResponse>> GetResult(Guid requestId)
    {
        var record = await _dbContext.MeasurementRecords
            .FirstOrDefaultAsync(r => r.RequestGuid == requestId);

        if (record == null)
            return NotFound(new { error = "Request not found" });

        if (record.Status == "pending" || record.Status == "processing")
            return BadRequest(new { error = "Not ready yet" });

        if (record.Status == "failed")
            return StatusCode(500, new ResultResponse
            {
                RequestId = requestId,
                Status = "failed",
                ErrorMessage = record.ErrorMessage
            });

        // Десериализуем JSON результат
        var result = JsonSerializer.Deserialize<ResultResponse>(record.ResultJson ?? "{}");
        result.RequestId = requestId;
        return Ok(result);
    }

    // Фоновая обработка (реальная)
    private async Task ProcessImageAsync(int recordId, CancellationToken token)
    {
        using var scope = _dbContext;
        var record = await scope.MeasurementRecords.FindAsync(recordId);
        if (record == null) return;

        try
        {
            // Обновляем статус
            record.Status = "processing";
            await scope.SaveChangesAsync(token);

            // ВАША РЕАЛЬНАЯ ОБРАБОТКА
            // var result = YourImageProcessor.Process(record.ImageData);

            // ПОКА МОК-ДАННЫЕ
            var mockResult = new
            {
                stones = new[]
                {
                    new { id = 1, coordinates = new[] { new[] { 10, 10 }, new[] { 50, 10 }, new[] { 30, 40 } }, areaPx = 450 }
                },
                totalTimeMs = 123
            };

            record.ResultJson = JsonSerializer.Serialize(mockResult);
            record.Status = "completed";
            record.CompletedAt = DateTime.UtcNow;
            record.ProcessedDate = DateTime.UtcNow; // ваше старое поле
        }
        catch (Exception ex)
        {
            record.Status = "failed";
            record.ErrorMessage = ex.Message;
            record.CompletedAt = DateTime.UtcNow;
        }

        await scope.SaveChangesAsync(token);
    }
}