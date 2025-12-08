using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace Quiz_Application_College.ViewModels
{
    public class ScheduleCreateVm
    {
        [Required]
        public Guid QuizId { get; set; }

        [Required]
        public DateTimeOffset StartAt { get; set; }

        [Required]
        public DateTimeOffset EndAt { get; set; }

        [Range(1, 10)]
        public int MaxAttempts { get; set; } = 1;

        [MaxLength(100)]
        public string? Timezone { get; set; }

        // Optional: Programming language filter for Coding quizzes
        public string? ProgrammingLanguage { get; set; }
        public List<SelectListItem> ProgrammingLanguageOptions { get; set; } = new List<SelectListItem>();
    }
}
