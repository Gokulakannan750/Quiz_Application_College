namespace Quiz_Application_College.ViewModels
{
    public class QuestionSelectVm
    {
        public Guid QuestionId { get; set; }
        public string Text { get; set; } = "";
        public bool Selected { get; set; }
        public int? Order { get; set; }   // optional: show current order if already in quiz
        public string? SourceFileName { get; set; }
    }
}
