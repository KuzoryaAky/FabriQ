using ImageProcessor.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ImageProcessor.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) {}

        public DbSet<MeasurementRecord> MeasurementRecords { get; set; }
    }
}
