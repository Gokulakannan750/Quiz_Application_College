namespace Quiz_Application_College.Domain.Coding
{
    public class CodeTestCase
    {
        public Guid Id { get; set; }

        public Guid CodeQuestionId { get; set; }
        public CodeQuestion CodeQuestion { get; set; } = default!;

        // Raw stdin-style input
        public string Input { get; set; } = "";

        // Expected stdout (trimmed compare in runner)
        public string ExpectedOutput { get; set; } = "";

        // True = hidden from student; False = visible/public
        public bool IsHidden { get; set; } = false;

        // Weight used to compute marks (sum passed weights / total weights * MaxMarks)
        public int Weight { get; set; } = 1;
    }
}
