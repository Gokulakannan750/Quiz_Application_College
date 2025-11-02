using System.ComponentModel.DataAnnotations.Schema;

namespace Quiz_Application_College.Domain
{
    public class QuizQuestion
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid QuizId { get; set; }
        [ForeignKey(nameof(QuizId))] public Quiz? Quiz { get; set; }

        public Guid QuestionId { get; set; }
        [ForeignKey(nameof(QuestionId))] public McqQuestion? Question { get; set; }

        public int Order { get; set; } = 1;
    }
}
