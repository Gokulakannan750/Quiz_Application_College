using System.ComponentModel.DataAnnotations;

namespace Quiz_Application_College.ViewModels
{
    public class QuizCreateVm
    {
        [Required, MaxLength(200)]
        public string Title { get; set; } = default!;

        [MaxLength(2000)]
        public string? Description { get; set; }

        [Range(1, 360)]
        public int DurationMinutes { get; set; } = 30;

        [Range(0, 1000)]
        public int TotalMarks { get; set; } = 100;

        public bool EnableNegativeMarking { get; set; } = false;

        [Range(0, 100)]
        public decimal? NegativeMarkPerWrong { get; set; }
        public bool ShuffleQuestions { get; set; } = true;
        public bool ShuffleOptions { get; set; } = true;
        public bool ShowReviewOnSubmit { get; set; } = true;
        public bool ShowScoreOnSubmit { get; set; } = true;

    }
}
