using System.ComponentModel.DataAnnotations;

namespace Quiz_Application_College.ViewModels
{
    public class EnrollmentCreateVm
    {
        [Required]
        public Guid QuizId { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; } = default!;
    }
}
