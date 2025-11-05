using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Quiz_Application_College.Domain.Coding
{
    public class CodeTestCase
    {
        public Guid Id { get; set; }

        // Foreign key to parent question
        public Guid CodeQuestionId { get; set; }

        // Navigation is NOT posted from the form; exclude it from model validation
        [ValidateNever]
        public CodeQuestion? CodeQuestion { get; set; }  // make nullable to avoid implicit [Required]

        // Raw stdin-style input
        public string Input { get; set; } = "";

        // Expected stdout (trimmed compare in runner)
        public string ExpectedOutput { get; set; } = "";

        // True = hidden from student; False = visible/public
        public bool IsHidden { get; set; } = false;

        // Weight used to compute marks (sum passed weights / total weights * MaxMarks)
        public int Weight { get; set; } = 1;
    }
}
