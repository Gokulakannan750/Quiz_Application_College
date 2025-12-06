using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Quiz_Application_College.Domain
{
    public class QuizSchedule
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid QuizId { get; set; }

        [ForeignKey(nameof(QuizId))]
        public Quiz? Quiz { get; set; }

        [Required]
        public DateTimeOffset StartAt { get; set; }

        [Required]
        public DateTimeOffset EndAt { get; set; }

        // Limit how many attempts a student can make within this window
        [Range(1, 10)]
        public int MaxAttempts { get; set; } = 1;

        // Optional timezone display (e.g., "Asia/Kolkata")
        [MaxLength(100)]
        public string? Timezone { get; set; }

        [Timestamp] public byte[]? RowVersion { get; set; }
    }
}
