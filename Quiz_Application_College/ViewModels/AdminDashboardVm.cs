using Quiz_Application_College.Domain;

namespace Quiz_Application_College.ViewModels
{
    public class AdminDashboardVm
    {
        // Summary cards
        public int TotalQuizzes { get; set; }
        public int OpenSchedulesNow { get; set; }
        public int TotalEnrollments { get; set; }
        public int AttemptsToday { get; set; }

        // Tables
        public List<EnrollmentRow> RecentEnrollments { get; set; } = new();
        public List<AttemptRow> RecentAttempts { get; set; } = new();

        public class EnrollmentRow
        {
            public Guid Id { get; set; }
            public Guid QuizId { get; set; }             // <— NEW
            public string Email { get; set; } = "";
            public string QuizTitle { get; set; } = "";
            public DateTimeOffset CreatedAt { get; set; }
            public string Status { get; set; } = "";
        }

        public class AttemptRow
        {
            public Guid Id { get; set; }
            public Guid QuizId { get; set; }             // <— NEW
            public string Email { get; set; } = "";
            public string QuizTitle { get; set; } = "";
            public DateTimeOffset StartedAt { get; set; }
            public DateTimeOffset? SubmittedAt { get; set; }
            public decimal Score { get; set; }
        }
    }
}
