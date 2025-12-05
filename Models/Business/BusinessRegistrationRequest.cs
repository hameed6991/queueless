namespace Queueless.Models.Business
{
    public class BusinessRegistrationRequest
    {
        public long UserId { get; set; }          // from VerifyOtp result

        public string BusinessName { get; set; } = null!;
        public string Category { get; set; } = null!;

        public string Emirate { get; set; } = null!;
        public string Area { get; set; } = null!;
        public string Building { get; set; } = null!;
        public string? Landmark { get; set; }

        public double Latitude { get; set; }
        public double Longitude { get; set; }

        public string ContactName { get; set; } = null!;
        public string ContactPhone { get; set; } = null!;
        public string? ContactWhatsapp { get; set; }

        public int? AvgTimeMinutes { get; set; }

        // For now we expect Flutter to send the image URL/path.
        // Later we will add file upload endpoint.
        public string? TradeLicenseImagePath { get; set; }
    }
}
