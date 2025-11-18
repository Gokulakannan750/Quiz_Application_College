using System.ComponentModel.DataAnnotations;

namespace Quiz_Application_College.Domain
{
    public enum QuizType
    {
        Mcq = 1,
        Coding = 2
    }

    public class Quiz
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required, MaxLength(200)]
        public string Title { get; set; } = default!;

        [MaxLength(2000)]
        public string? Description { get; set; }

        // Total minutes allowed for an attempt
        [Range(1, 360)]
        public int DurationMinutes { get; set; } = 30;

        // total marks (for display/normalization)
        [Range(0, 1000)]
        public int TotalMarks { get; set; } = 100;

        // NEW: language folder label for coding quizzes (C, Python, Java, etc.)
        public string? ProgrammingLanguage { get; set; }

        public bool EnableNegativeMarking { get; set; } = false;

        // if enabled, how much to deduct per wrong MCQ (can be 0.25 etc.)
        public decimal? NegativeMarkPerWrong { get; set; }

        // soft flags
        public bool IsPublished { get; set; } = false;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? UpdatedAt { get; set; }

        [Timestamp] public byte[]? RowVersion { get; set; }
        public bool ShuffleQuestions { get; set; } = true;
        public bool ShuffleOptions { get; set; } = true;
        public bool ShowReviewOnSubmit { get; set; } = true;   // allows the Review page
        public bool ShowScoreOnSubmit { get; set; } = true;   // see score immediately after submit

        public QuizType Type { get; set; } = QuizType.Mcq;

    }
}
