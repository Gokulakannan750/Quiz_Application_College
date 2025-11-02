using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Quiz_Application_College.Domain
{
    // Enrollment gives a specific user access to a specific quiz (irrespective of schedule)
    public class Enrollment
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid QuizId { get; set; }

        [ForeignKey(nameof(QuizId))]
        public Quiz? Quiz { get; set; }

        // IdentityUser key type is string
        [Required, MaxLength(450)]
        public string UserId { get; set; } = default!;

        // future: Active, Revoked, Completed
        [MaxLength(40)]
        public string Status { get; set; } = "Active";

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        [Timestamp] public byte[]? RowVersion { get; set; }
    }
}
