using System.Net;
using ImageProcessor.Services;
using ImageProcessor.Services.Interfaces;
using Microsoft.OpenApi;

namespace ImageProcessor
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);


            // 5. Включаем CORS перед авторизацией
            // 1. Добавляем контроллеры
            builder.Services.AddControllers();

            // 2. Регистрируем наш сервис
            builder.Services.AddScoped<IImageProcessingService, ImageProcessingService>();
            builder.Services.AddEndpointsApiExplorer();

            // 3. Настройка CORS для мобильного приложения
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("MobileApp", policy =>
                {
                    policy.AllowAnyOrigin()  // Можно заменить на конкретные адреса
                          .AllowAnyMethod()
                          .AllowAnyHeader()
                          .WithExposedHeaders("Content-Disposition") // Для скачивания файлов
                          .SetPreflightMaxAge(TimeSpan.FromHours(1));
                });
            });

            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
                {
                    Title = "Image Processor API",
                    Version = "v1",
                    Description = "API для измерения размеров каменных плит"
                });
            });

            var app = builder.Build();

            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Image Processor API v1");
                c.RoutePrefix = string.Empty;
            }
            );

            // 4. Настраиваем маршрутизацию
            app.UseHttpsRedirection();
            app.UseAuthorization();
            app.UseCors("MobileApp");

            // Добавь эту строку для доступа к файлам в wwwroot
            app.UseStaticFiles();

            app.MapControllers();

            // 5. Запускаем приложение
            app.Run();
        }
    }
}




//using System.Text;
//using ImageProcessor.Services;
//using ImageProcessor.Services.Interfaces;
//using Microsoft.AspNetCore.Authentication.JwtBearer;
//using Microsoft.IdentityModel.Tokens;

//var builder = WebApplication.CreateBuilder(args);

//// Автоматическое определение порта
//var port = Environment.GetEnvironmentVariable("ASPNETCORE_URLS")?.Split(':').LastOrDefault() ?? "80";
//builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

////Аутентификация
//builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
//    .AddJwtBearer(options =>
//    {
//        options.TokenValidationParameters = new TokenValidationParameters
//        {
//            ValidateIssuer = true,
//            ValidateAudience = true,
//            ValidateLifetime = true,
//            ValidateIssuerSigningKey = true,
//            ValidIssuer = "your-issuer",
//            ValidAudience = "your-audience",
//            IssuerSigningKey = new SymmetricSecurityKey(
//                Encoding.UTF8.GetBytes("your-super-secret-key"))
//        };
//    });

//// Добавляем сервисы
//builder.Services.AddControllers();
//builder.Services.AddHealthChecks();

//// Регистрация кастомных сервисов
//builder.Services.AddScoped<IImageProcessingService, ImageProcessingService>();

//var app = builder.Build();

//// Настройка middleware
//if (app.Environment.IsDevelopment())
//{
//    app.UseDeveloperExceptionPage();
//}

//app.UseRouting();

//// Health check endpoint для Docker
//app.MapHealthChecks("/health");
//app.MapHealthChecks("/ready");

//// Информация о контейнере
//app.MapGet("/container-info", () =>
//{
//    var containerId = Environment.GetEnvironmentVariable("HOSTNAME") ?? "unknown";
//    var isDocker = !string.IsNullOrEmpty(containerId) && containerId.Length == 64;

//    return Results.Json(new
//    {
//        runningInDocker = isDocker,
//        containerId = containerId,
//        hostname = Environment.MachineName,
//        os = Environment.OSVersion.VersionString,
//        processorCount = Environment.ProcessorCount,
//        memory = GetMemoryUsage(),
//        environment = app.Environment.EnvironmentName,
//        urls = builder.WebHost.GetSetting(WebHostDefaults.ServerUrlsKey),
//        timestamp = DateTime.UtcNow
//    });
//});

//// Простой endpoint для теста
//app.MapGet("/", () =>
//{
//    var hostname = Environment.MachineName;
//    return Results.Ok($"🚀 Image Processor API запущен в Docker!\n📦 Контейнер: {hostname}\n⏰ Время: {DateTime.Now}");
//});

//app.MapControllers();

//Console.WriteLine("=========================================");
//Console.WriteLine($"🐳 Запуск в Docker контейнере");
//Console.WriteLine($"🌐 Порт: {port}");
//Console.WriteLine($"📦 Container ID: {Environment.GetEnvironmentVariable("HOSTNAME")}");
//Console.WriteLine("=========================================");

//app.Run();

//static object GetMemoryUsage()
//{
//    try
//    {
//        var process = System.Diagnostics.Process.GetCurrentProcess();
//        return new
//        {
//            usedMB = Math.Round(process.WorkingSet64 / 1024.0 / 1024.0, 2),
//            peakMB = Math.Round(process.PeakWorkingSet64 / 1024.0 / 1024.0, 2)
//        };
//    }
//    catch
//    {
//        return "недоступно";
//    }
//}


