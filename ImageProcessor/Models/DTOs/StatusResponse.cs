namespace ImageProcessor.Models.DTOs
{
    public class StatusResponse
    {
        public Guid RequestId { get; set; }
        public string Status { get; set; } = "pending"; // pending, processing, completed, failed
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
