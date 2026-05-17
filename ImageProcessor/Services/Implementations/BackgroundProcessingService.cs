using ImageProcessor.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ImageProcessor.Services.Implementations
{
    public class BackgroundProcessingService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BackgroundProcessingService> _logger;

        public BackgroundProcessingService(IServiceProvider serviceProvider, ILogger<BackgroundProcessingService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await ProcessPendingRequests(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); // проверяем каждые 2 секунды
            }
        }

        private async Task ProcessPendingRequests(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var imageProcessor = scope.ServiceProvider.GetRequiredService<Interfaces.IImageProcessingService>(); // ваш существующий

            // Берём первый pending запрос
            var pendingRequest = await dbContext.MeasurementRecords
                .Where(r => r.Status == "pending")
                .OrderBy(r => r.CreatedAt)
                .FirstOrDefaultAsync(stoppingToken);

            if (pendingRequest == null) return;

            try
            {
                // Меняем статус на processing
                pendingRequest.Status = "processing";
                await dbContext.SaveChangesAsync(stoppingToken);

                _logger.LogInformation($"Processing request {pendingRequest.Id}");

                // Заглушка: здесь вызывается ваша реальная обработка
                // var result = await imageProcessor.ProcessImage(pendingRequest.ImagePath);

                // ВРЕМЕННО: моковый результат (пока детекция не готова)
                var mockResult = new
                {
                    stones = new[]
                    {
                        new { id = 1, coordinates = new[] { new[] { 10, 10 }, new[] { 50, 10 }, new[] { 30, 40 } }, areaPx = 450.0 }
                    },
                    totalTimeMs = 123
                };

                // Сохраняем результат как JSON
                pendingRequest.ResultJson = System.Text.Json.JsonSerializer.Serialize(mockResult);
                pendingRequest.Status = "completed";
                pendingRequest.CompletedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing request {pendingRequest.Id}");
                pendingRequest.Status = "failed";
                pendingRequest.ErrorMessage = ex.Message;
                pendingRequest.CompletedAt = DateTime.UtcNow;
            }

            await dbContext.SaveChangesAsync(stoppingToken);
        }
    }
}