using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Queueless.Models.Business
{
    [Table("BusinessRegistration")]
    public class BusinessRegistration
    {
        [Key]
        public int BusinessId { get; set; }

        // later you can link with AppUser
        public long? OwnerUserId { get; set; }

        // Basic details
        [Required]
        [MaxLength(200)]
        public string BusinessName { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string Category { get; set; } = null!;

        // Address
        [Required]
        [MaxLength(50)]
        public string Emirate { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string Area { get; set; } = null!;

        [Required]
        [MaxLength(200)]
        public string BuildingName { get; set; } = null!;

        [MaxLength(200)]
        public string? Landmark { get; set; }

        // Map
        [Required]
        public decimal Latitude { get; set; }

        [Required]
        public decimal Longitude { get; set; }

        // Contact
        [Required]
        [MaxLength(100)]
        public string ContactPersonName { get; set; } = null!;

        [Required]
        [MaxLength(20)]
        public string ContactMobile { get; set; } = null!;

        [MaxLength(20)]
        public string? ContactWhatsapp { get; set; }

        // Service details
        public int? AvgTimeMinutes { get; set; }

        // Trade licence image path (URL or local path)
        [MaxLength(500)]
        public string? TradeLicenseImagePath { get; set; }

        // system fields
        public DateTime CreatedOn { get; set; } = DateTime.Now;

        [MaxLength(100)]
        public string? CreatedBy { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
