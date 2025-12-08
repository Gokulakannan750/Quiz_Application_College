using System;

namespace Quiz_Application_College.ViewModels
{
    public class EnrollmentSummaryVm
    {
        public Guid QuizId { get; set; }
        public string QuizTitle { get; set; } = "";
        public string College { get; set; } = "";
        public string Department { get; set; } = "";
        public int TotalStudents { get; set; }
    }
}
