namespace Quiz_Application_College.ViewModels.Coding
{
    public class SaveCodeDto
    {
        public Guid AttemptId { get; set; }
        public Guid CodeQuestionId { get; set; }
        public string Language { get; set; } = "python";
        public string SourceCode { get; set; } = "";
    }
}
