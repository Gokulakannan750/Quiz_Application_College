using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Quiz_Application_College.Domain.Coding
{
    public class CodingQuizFolder
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = default!;

        [MaxLength(500)]
        public string? Description { get; set; }

        public int OrderNo { get; set; }

        // All CODING quizzes that belong to this folder
        public ICollection<Quiz> Quizzes { get; set; } = new List<Quiz>();
    }
}
