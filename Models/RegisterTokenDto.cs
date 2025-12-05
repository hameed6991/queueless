namespace Queueless.Models
{
    public class RegisterTokenDto
    {
        public int UserId { get; set; }
        public string FcmToken { get; set; } = "";
        public string? Platform { get; set; }
    }
}
