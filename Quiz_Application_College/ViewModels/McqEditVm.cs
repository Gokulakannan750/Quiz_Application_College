using System.ComponentModel.DataAnnotations;

namespace Quiz_Application_College.ViewModels
{
    public class McqEditVm
    {
        public Guid Id { get; set; }

        [Required, MaxLength(1000)]
        public string Text { get; set; } = "";

        [Range(0, 1000)]
        public decimal Marks { get; set; } = 1m;

        public List<OptionVm> Options { get; set; } = new();

        public class OptionVm
        {
            public Guid? Id { get; set; }          // existing option has Id; new ones are null
            [Required, MaxLength(500)]
            public string Text { get; set; } = "";
            public bool IsCorrect { get; set; }
        }
    }
}
