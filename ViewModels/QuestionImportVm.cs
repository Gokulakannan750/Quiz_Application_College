using Microsoft.AspNetCore.Mvc.Rendering;

namespace Quiz_Application_College.ViewModels
{
    public class QuestionImportVm
    {
        // Upload result summary
        public int TotalRows { get; set; }
        public int Inserted { get; set; }
        public int Skipped { get; set; }
        public List<string> Errors { get; set; } = new();
        public string? FileName { get; set; }
        public bool HasResult => TotalRows > 0 || Errors.Count > 0;

        // NEW: bulk assign configuration
        public bool AssignToQuiz { get; set; } = false;
        public Guid? QuizId { get; set; }
        public IEnumerable<SelectListItem> Quizzes { get; set; } = Enumerable.Empty<SelectListItem>();
    }
}
