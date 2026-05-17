using System;
using System.Collections.Generic;

namespace FabriQ.Models.DTOs
{
    public class ResultResponse
    {
        public Guid RequestId { get; set; }
        public string Status { get; set; } = "completed";
        public List<StoneDto> Stones { get; set; } = new();
        public double TotalTimeMs { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class StoneDto
    {
        public int Id { get; set; }
        public List<List<int>> Coordinates { get; set; } = new(); // [[x,y], [x,y], ...]
        public double AreaPx { get; set; }
        public double? AreaMm { get; set; }
        public string? Material { get; set; }
    }
}
