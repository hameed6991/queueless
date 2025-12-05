namespace Queueless.Models.Auth
{
    public class RequestOtpDto
    {
        public string MobileNumber { get; set; } = string.Empty;
    }

    public class RequestOtpResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsNewUser { get; set; }
        public string? DebugOtp { get; set; }   // only for testing
    }

    public class VerifyOtpDto
    {
        public string MobileNumber { get; set; } = string.Empty;
        public string Otp { get; set; } = string.Empty;   // 👈 IMPORTANT: property name = Otp
    }

    public class VerifyOtpResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public long UserId { get; set; }
        public bool IsNewUser { get; set; }
        public bool HasBusiness { get; set; }
    }
}
