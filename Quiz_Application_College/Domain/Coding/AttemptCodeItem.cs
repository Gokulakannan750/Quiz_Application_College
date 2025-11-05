using Quiz_Application_College.Domain;

namespace Quiz_Application_College.Domain.Coding
{
    // One row per (Attempt, CodeQuestion)
    public class AttemptCodeItem
    {
        public Guid Id { get; set; }

        public Guid AttemptId { get; set; }
        public Attempt Attempt { get; set; } = default!;

        public Guid CodeQuestionId { get; set; }
        public CodeQuestion CodeQuestion { get; set; } = default!;

        // Last chosen language (e.g., "python", "csharp")
        public string Language { get; set; } = "python";

        // Last saved editor content
        public string SourceCode { get; set; } = "";

        // Computed at last run
        public int PassedCount { get; set; } = 0;
        public int TotalCount { get; set; } = 0;
        public decimal MarksAwarded { get; set; } = 0m;

        public DateTimeOffset? LastRunAt { get; set; }
    }
}
