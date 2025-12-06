using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Quiz_Application_College.Domain
{
    public class McqOption
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid QuestionId { get; set; }

        [ForeignKey(nameof(QuestionId))]
        public McqQuestion? Question { get; set; }

        [Required, MaxLength(2000)]
        public string Text { get; set; } = default!;

        // Exactly one option per question should be true
        public bool IsCorrect { get; set; }
    }
}
