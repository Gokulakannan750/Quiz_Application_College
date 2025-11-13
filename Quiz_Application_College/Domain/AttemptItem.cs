using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Quiz_Application_College.Domain
{
    public class AttemptItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid AttemptId { get; set; }

        [ForeignKey(nameof(AttemptId))]
        public Attempt? Attempt { get; set; }

        [Required]
        public Guid QuestionId { get; set; }

        [ForeignKey(nameof(QuestionId))]
        public McqQuestion? Question { get; set; }

        // 1-based position in this attempt
        public int Order { get; set; }

        // JSON array of option IDs in the display order for this attempt
        [Required]
        public string OptionOrderJson { get; set; } = "[]";

        // Marks for this question in this attempt
        [Column(TypeName = "decimal(10,2)")]
        public decimal MarksAwarded { get; set; }
    }
}
