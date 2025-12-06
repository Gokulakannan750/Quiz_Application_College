namespace Quiz_Application_College.ViewModels
{
    public class ExamReviewVm
    {
        public string QuizTitle { get; set; } = "";
        public decimal TotalScore { get; set; }
        public List<Item> Items { get; set; } = new();

        public class Item
        {
            public int Number { get; set; }
            public string Question { get; set; } = "";
            public List<Option> Options { get; set; } = new();
            public Guid? YourOptionId { get; set; }
            public Guid? CorrectOptionId { get; set; }
            public decimal Marks { get; set; }
            public decimal Earned { get; set; }
        }

        public class Option
        {
            public Guid Id { get; set; }
            public string Text { get; set; } = "";
        }
    }
}
