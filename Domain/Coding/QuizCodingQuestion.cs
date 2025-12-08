namespace Quiz_Application_College.Domain.Coding
{
    public class QuizCodingQuestion
    {
        public Guid QuizId { get; set; }
        public Quiz Quiz { get; set; } = default!;

        public Guid CodeQuestionId { get; set; }
        public CodeQuestion CodeQuestion { get; set; } = default!;

        // Optional order field for display
        public int Order { get; set; } = 0;
    }
}
