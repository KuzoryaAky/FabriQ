namespace ImageProcessor.Models.Entities
{
    public class MeasurementRecord
    {
        public int Id { get; set; }
        public required string FileName { get; set; }
        public required byte[] ImageData { get; set; }
        public DateTime? ProcessedDate { get; set; }

        public string Status { get; set; } = "pending"; // pending, processing, completed, failed
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        public string? ResultJson { get; set; }  // JSON с результатами обработки
        public string? ErrorMessage { get; set; }
        public Guid RequestGuid { get; set; } = Guid.NewGuid(); // для API (внешний ID)
    }
}
