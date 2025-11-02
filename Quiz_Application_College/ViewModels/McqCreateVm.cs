using System.ComponentModel.DataAnnotations;

namespace Quiz_Application_College.ViewModels
{
    public class McqCreateVm
    {
        [Required, MaxLength(2000)]
        public string Text { get; set; } = default!;

        [Range(0, 1000)]
        public decimal Marks { get; set; } = 1m;

        [MaxLength(100)]
        public string? Tag { get; set; }

        // exactly 4 inputs for simplicity now
        public string?[] Options { get; set; } = new string?[4];

        public int? CorrectIndex { get; set; }
    }
}
