namespace Quiz_Application_College.ViewModels.Coding
{
    public class EnrollmentRowVm
    {
        public Guid Id { get; set; }
        public Guid QuizId { get; set; }
        public string QuizTitle { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string Status { get; set; } = "Active";
        public DateTimeOffset CreatedAt { get; set; }
    }
}
