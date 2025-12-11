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

        // optional: you’re not really using IsNewUser yet, but we keep it
        public bool IsNewUser { get; set; }

        // for debugging only (so you can see the OTP / error in Swagger/Postman)
        public string? DebugOtp { get; set; }
    }
    public class VerifyOtpRequestDto
    {
        public string MobileNumber { get; set; } = string.Empty;
        public string Otp { get; set; } = string.Empty;

        // From app: "Customer" or "Business"
        public string? LoginType { get; set; }
    }

    public class VerifyOtpResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;

        public long UserId { get; set; }
        public bool IsNewUser { get; set; }

        public bool HasBusiness { get; set; }

        // "Customer" / "Business" / "Both" (whatever is in AppUser.Role)
        public string Role { get; set; } = "Customer";
    }
}
