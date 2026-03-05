namespace ImageProcessor.Models.Responses
{
    public class MeasurementResponse
    {
        public bool Success { get; set; } = true;
        public double WidthMm { get; set; }    // Ширина в мм
        public double HeightMm { get; set; }   // Высота в мм
        public string Error { get; set; }      // Если ошибка

        // Добавьте эти поля для отладки
        public string? RequestId { get; set; }
        public DateTime? ProcessingTime { get; set; }
        public string? FileName { get; set; }
        public long? FileSizeBytes { get; set; }

        // Если есть детали детекции
        public double? Confidence { get; set; }
        public string? MaterialType { get; set; }
    }
}
