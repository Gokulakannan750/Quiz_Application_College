using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Quiz_Application_College.Domain
{
    public class McqQuizFolder
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = default!;

        [MaxLength(500)]
        public string? Description { get; set; }

        // For sorting folders in the list
        public int OrderNo { get; set; }

        // All MCQ quizzes that belong to this folder
        public ICollection<Quiz> Quizzes { get; set; } = new List<Quiz>();
    }
}
