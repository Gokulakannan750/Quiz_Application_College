using System.ComponentModel.DataAnnotations;

namespace Quiz_Application_College.Domain
{
    public class StudentProfile
    {
        public Guid Id { get; set; }

        // Legacy link to Identity user – now optional
        public string? UserId { get; set; }

        [Required, MaxLength(256)]
        public string Email { get; set; } = "";

        [MaxLength(100)]
        public string Name { get; set; } = "";

        [MaxLength(50)]
        public string RollNumber { get; set; } = "";

        [MaxLength(100)]
        public string College { get; set; } = "";

        [MaxLength(100)]
        public string Department { get; set; } = "";

        [Required]
        public string PasswordHash { get; set; } = ""; // base64

        [Required, MaxLength(200)]
        public string PasswordSalt { get; set; } = ""; // base64

        public bool IsActive { get; set; } = true;

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
    }
}
