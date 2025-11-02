using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Quiz_Application_College.Domain
{
    public class Attempt
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required] public Guid QuizId { get; set; }
        [ForeignKey(nameof(QuizId))] public Quiz? Quiz { get; set; }

        [Required, MaxLength(450)]
        public string UserId { get; set; } = default!;

        public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? SubmittedAt { get; set; }

        // computed total after auto-eval (MCQ) + manual (coding) if any
        public decimal Score { get; set; } = 0m;

        // store device fingerprint or user-agent snapshot for audit
        [MaxLength(500)]
        public string? DeviceFingerprint { get; set; }

        // friendly flags
        public bool IsSubmitted => SubmittedAt.HasValue;

        [Timestamp] public byte[]? RowVersion { get; set; }
    }
}
