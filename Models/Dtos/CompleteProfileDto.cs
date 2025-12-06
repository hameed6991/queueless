namespace Queueless.Models.Dtos
{
    public class CompleteProfileDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public bool IsBusinessOwner { get; set; }
    }
}
