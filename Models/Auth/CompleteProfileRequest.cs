namespace Queueless.Models.Auth
{
    public class CompleteProfileRequest
    {
        public long UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool IsBusinessOwner { get; set; }
    }

    public class CompleteProfileResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool HasBusiness { get; set; }
    }
}
