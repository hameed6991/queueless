// Models/Business/NearbyBusinessDto.cs
namespace Queueless.Models.Business
{
    public class NearbyBusinessDto
    {
        public int BusinessId { get; set; }

        public string BusinessName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Emirate { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
        public string BuildingName { get; set; } = string.Empty;

        // optional
        public string? Landmark { get; set; }

        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }

        public double DistanceKm { get; set; }

        // 0 if null in DB
        public int WaitingCount { get; set; }

        // Nullable – may be null in DB
        public int? EstimatedWaitMinutes { get; set; }
        public int? AvgTimeMinutes { get; set; }
    }
}
