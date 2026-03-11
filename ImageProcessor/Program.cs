using System.Net;
using ImageProcessor.Infrastructure.Data;
using ImageProcessor.Services;
using ImageProcessor.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

namespace ImageProcessor
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

            builder.Services.AddControllers();

            builder.Services.AddScoped<IImageProcessingService, ImageProcessingService>();
            builder.Services.AddEndpointsApiExplorer();

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("MobileApp", policy =>
                {
                    policy.AllowAnyOrigin()  
                          .AllowAnyMethod()
                          .AllowAnyHeader()
                          .WithExposedHeaders("Content-Disposition") 
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

            app.UseHttpsRedirection();
            app.UseAuthorization();
            app.UseCors("MobileApp");

            app.UseStaticFiles();

            app.MapControllers();

            app.Run();
        }
    }
}