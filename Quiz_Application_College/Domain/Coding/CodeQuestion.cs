using System.ComponentModel.DataAnnotations;

namespace Quiz_Application_College.Domain.Coding
{
    public class CodeQuestion
    {
        public Guid Id { get; set; }

        [Required, MaxLength(160)]
        public string Title { get; set; } = "";

        // Problem statement (markdown allowed in UI; keep plain text here)
        [Required]
        public string Prompt { get; set; } = "";

        // Max score for this coding question
        public decimal MaxMarks { get; set; } = 10m;

        // CSV: e.g., "csharp,cpp,python,java,js"
        [Required, MaxLength(200)]
        public string AllowedLanguagesCsv { get; set; } = "csharp,python";

        // Optional starter code per language: JSON: { "csharp": "...", "python":"..." }
        public string? StarterCodeJson { get; set; }

        public ICollection<CodeTestCase> TestCases { get; set; } = new List<CodeTestCase>();
    }
}
