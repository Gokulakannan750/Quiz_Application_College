using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Quiz_Application_College.Domain
{
    public enum QuestionType
    {
        Mcq = 1,
        Coding = 2
    }

    // Stores the user's answer to a specific question inside an Attempt
    public class Response
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required] public Guid AttemptId { get; set; }
        [ForeignKey(nameof(AttemptId))] public Attempt? Attempt { get; set; }

        // Your legacy bank may have integer IDs; keep Guid for new questions later.
        [Required] public Guid QuestionId { get; set; }

        public QuestionType Type { get; set; } = QuestionType.Mcq;

        // Keep raw payload flexible (MCQ: chosen option; Coding: source, language, stdout, stderr)
        public string ResponseJson { get; set; } = "{}";

        public decimal Score { get; set; } = 0m;
        public DateTimeOffset? EvaluatedAt { get; set; }

        [Timestamp] public byte[]? RowVersion { get; set; }
    }
}
