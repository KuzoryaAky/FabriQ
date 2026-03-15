namespace ImageProcessor.Models.Entities
{
    public class MeasurementRecord
    {
        public int Id { get; set; }
        public required string FileName { get; set; }
        public required byte[] ImageData { get; set; }
        public DateTime? ProcessedDate { get; set; }
    }
}
