using ImageProcessor.Models.Requests;
using ImageProcessor.Models.Responses;

namespace ImageProcessor.Services.Interfaces
{
    public interface IImageProcessingService
    {
        Task<MeasurementResponse> ProcessImageAsync(MeasurementRequest request);
    }
}
