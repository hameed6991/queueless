namespace Queueless.Models
{
    public class UserOtp
    {
        public long Id { get; set; }
        public string MobileNumber { get; set; } = null!;
        public string OtpCode { get; set; } = null!;
        public DateTime ExpiresAtUtc { get; set; }
        public int AttemptCount { get; set; }
    }
}
