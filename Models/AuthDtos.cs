namespace Queueless.Models
{
    public class RequestOtpDto
    {
        public string MobileNumber { get; set; } = string.Empty;
    }

    public class VerifyOtpRequestDto
    {
        public string MobileNumber { get; set; } = string.Empty;
        public string Otp { get; set; } = string.Empty;
    }

    public class VerifyOtpResponseDto
    {
        public bool Success { get; set; }
        public long? UserId { get; set; }
        public bool IsNewUser { get; set; }
        public bool HasBusiness { get; set; }
        public int ErrorCode { get; set; }
        public string? Message { get; set; }
    }
}