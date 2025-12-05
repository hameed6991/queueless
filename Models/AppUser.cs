using System;
using System.ComponentModel.DataAnnotations;

namespace Queueless.Models   // or whatever namespace you used
{
    public class AppUser
    {
        [Key]                         // 👈 tell EF this is the PK
        public long UserId { get; set; }

        [Required]
        [MaxLength(20)]
        public string MobileNumber { get; set; } = null!;

        [MaxLength(100)]
        public string? FullName { get; set; }

        [MaxLength(150)]
        public string? Email { get; set; }

        [Required]
        [MaxLength(20)]
        public string Role { get; set; } = "Customer";   // Customer / Customer+Business / Admin

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAtUtc { get; set; }

        public DateTime? LastLoginUtc { get; set; }
    }
}
