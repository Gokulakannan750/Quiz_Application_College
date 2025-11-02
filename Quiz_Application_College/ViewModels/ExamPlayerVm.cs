using Quiz_Application_College.Domain;

namespace Quiz_Application_College.ViewModels
{
    public class ExamPlayerVm
    {
        public Guid AttemptId { get; set; }
        public string QuizTitle { get; set; } = default!;
        public int DurationMinutes { get; set; }
        public DateTimeOffset StartedAt { get; set; }

        public List<QuestionVm> Questions { get; set; } = new();
        public Dictionary<Guid, Guid?> Answers { get; set; } = new(); // QuestionId -> chosen OptionId

        public class QuestionVm
        {
            public Guid QuestionId { get; set; }
            public string Text { get; set; } = default!;
            public List<OptionVm> Options { get; set; } = new();
        }

        public class OptionVm
        {
            public Guid OptionId { get; set; }
            public string Text { get; set; } = default!;
        }
    }

    public class SaveAnswerRequest
    {
        public Guid AttemptId { get; set; }
        public Guid QuestionId { get; set; }
        public Guid? OptionId { get; set; }
    }
}
