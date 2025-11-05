using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Quiz_Application_College.Domain;
using Quiz_Application_College.Domain.Coding;
using System.Reflection.Emit;

namespace Quiz_Application_College.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<Quiz> Quizzes => Set<Quiz>();
        public DbSet<QuizSchedule> QuizSchedules => Set<QuizSchedule>();
        public DbSet<Enrollment> Enrollments => Set<Enrollment>();
        public DbSet<Attempt> Attempts => Set<Attempt>();
        public DbSet<Response> Responses => Set<Response>();
        public DbSet<McqQuestion> McqQuestions => Set<McqQuestion>();
        public DbSet<McqOption> McqOptions => Set<McqOption>();
        public DbSet<QuizQuestion> QuizQuestions => Set<QuizQuestion>();
        public DbSet<AttemptItem> AttemptItems => Set<AttemptItem>();
        public DbSet<CodeQuestion> CodeQuestions => Set<CodeQuestion>();
        public DbSet<CodeTestCase> CodeTestCases => Set<CodeTestCase>();
        public DbSet<AttemptCodeItem> AttemptCodeItems => Set<AttemptCodeItem>();


        protected override void OnModelCreating(ModelBuilder b)
        {
            base.OnModelCreating(b);

            // Quiz
            b.Entity<Quiz>()
                .HasIndex(q => q.Title);

            // QuizSchedule
            b.Entity<QuizSchedule>()
                .HasIndex(s => new { s.QuizId, s.StartAt, s.EndAt });

            // Enrollment
            b.Entity<Enrollment>()
                .HasIndex(e => new { e.QuizId, e.UserId })
                .IsUnique();

            // Attempt
            b.Entity<Attempt>()
                .HasIndex(a => new { a.QuizId, a.UserId, a.StartedAt });

            // Response
            b.Entity<Response>()
                .HasIndex(r => new { r.AttemptId, r.QuestionId })
                .IsUnique();

            // McqQuestion
            b.Entity<McqQuestion>()
                .HasIndex(q => q.NormalizedText)
                .IsUnique()
                .HasFilter("[NormalizedText] IS NOT NULL");

            // McqOption
            b.Entity<McqOption>()
                .HasIndex(o => new { o.QuestionId, o.IsCorrect });

            // QuizQuestion
            b.Entity<QuizQuestion>()
                .HasIndex(qq => new { qq.QuizId, qq.QuestionId })
                .IsUnique();

            // QuizQuestion - Order
            b.Entity<QuizQuestion>()
                .HasIndex(qq => new { qq.QuizId, qq.Order });

            b.Entity<AttemptItem>()
                .HasIndex(ai => new { ai.AttemptId, ai.Order })
                .IsUnique();

            b.Entity<AttemptItem>()
             .HasIndex(ai => new { ai.AttemptId, ai.QuestionId })
             .IsUnique();

            b.Entity<Attempt>()
             .Property(a => a.Score)
             .HasPrecision(18, 2);

            b.Entity<McqQuestion>()
             .Property(q => q.Marks)
             .HasPrecision(18, 2);

            // If your Quiz has TotalMarks and NegativeMarkPerWrong decimals:
            b.Entity<Quiz>()
             .Property(q => q.TotalMarks)
             .HasPrecision(18, 2);

            b.Entity<Quiz>()
             .Property(q => q.NegativeMarkPerWrong)
             .HasPrecision(18, 2);

            // If your Response has a decimal Score column (only add if it exists in your model):
            b.Entity<Response>()
             .Property(r => r.Score)
             .HasPrecision(18, 2);

            b.Entity<CodeTestCase>()
                 .HasOne(t => t.CodeQuestion)
                 .WithMany(q => q.TestCases)
                 .HasForeignKey(t => t.CodeQuestionId)
                 .OnDelete(DeleteBehavior.Cascade);

            b.Entity<AttemptCodeItem>()
                .HasIndex(x => new { x.AttemptId, x.CodeQuestionId })
                .IsUnique(); // one coding row per question in an attempt

            b.Entity<Quiz_Application_College.Domain.Coding.CodeQuestion>()
                .Property(q => q.MaxMarks)
                .HasPrecision(10, 2); // up to 99999999.99

            b.Entity<Quiz_Application_College.Domain.Coding.AttemptCodeItem>()
                .Property(a => a.MarksAwarded)
                .HasPrecision(10, 2);
        }
    }
}
