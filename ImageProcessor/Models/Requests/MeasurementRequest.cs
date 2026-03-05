using System.ComponentModel.DataAnnotations;

namespace ImageProcessor.Models.Requests
{
    public class MeasurementRequest
    {
        [Required(ErrorMessage = "Image is required 1")]
        public IFormFile Image { get; set; }

        //[Required(ErrorMessage = "Reference type is required")]
        //public string ReferenceType { get; set; } = "A4";

        //public string UserId { get; set; } = "test-user"; // Временное значение
    }
}
