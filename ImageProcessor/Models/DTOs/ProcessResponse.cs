namespace ImageProcessor.Models.DTOs
{
    public class ProcessResponse
    {
        public Guid RequestId { get; set; }
        public string Status { get; set; } = "pending";
        public string Message { get; set; } = "Request accepted. Use /status/{id} to check progress.";
    }
}
