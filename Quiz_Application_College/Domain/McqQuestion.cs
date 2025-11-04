using System.ComponentModel.DataAnnotations;

namespace Quiz_Application_College.Domain
{
    public class McqQuestion
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required, MaxLength(2000)]
        public string Text { get; set; } = default!;

        [Range(0, 1000)]
        public decimal Marks { get; set; } = 1m;

        public ICollection<McqOption> Options { get; set; } = new List<McqOption>();

        // convenience for manual difficulty tags etc. (optional)
        [MaxLength(100)]
        public string? Tag { get; set; }

        // Add this new column to hold a normalized, dedupe key
        public string? NormalizedText { get; set; }  // lowercase, spaces collapsed

    }
}
