using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Quiz_Application_College.Domain;
using Quiz_Application_College.Domain.Coding;

namespace Quiz_Application_College.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        // Core
        public DbSet<Quiz> Quizzes => Set<Quiz>();
        public DbSet<QuizSchedule> QuizSchedules => Set<QuizSchedule>();
        public DbSet<Enrollment> Enrollments => Set<Enrollment>();
        public DbSet<Attempt> Attempts => Set<Attempt>();
        public DbSet<Response> Responses => Set<Response>();
        public DbSet<McqQuestion> McqQuestions => Set<McqQuestion>();
        public DbSet<McqOption> McqOptions => Set<McqOption>();
        public DbSet<QuizQuestion> QuizQuestions => Set<QuizQuestion>();
        public DbSet<AttemptItem> AttemptItems => Set<AttemptItem>();

        // Coding
        public DbSet<CodeQuestion> CodeQuestions => Set<CodeQuestion>();
        public DbSet<CodeTestCase> CodeTestCases => Set<CodeTestCase>();
        public DbSet<AttemptCodeItem> AttemptCodeItems => Set<AttemptCodeItem>();
        public DbSet<QuizCodingQuestion> QuizCodingQuestions => Set<QuizCodingQuestion>();
        public DbSet<Quiz_Application_College.Domain.StudentProfile> StudentProfiles { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder b)
        {
            base.OnModelCreating(b);

            // ===== Quizzes & Schedules =====
            b.Entity<Quiz>()
                .HasIndex(q => q.Title);

            b.Entity<QuizSchedule>()
                .HasIndex(s => new { s.QuizId, s.StartAt, s.EndAt });

            // ===== Enrollments =====
            b.Entity<Enrollment>()
                .HasIndex(e => new { e.QuizId, e.UserId })
                .IsUnique();

            // ===== Attempts =====
            b.Entity<Attempt>()
                .HasIndex(a => new { a.QuizId, a.UserId, a.StartedAt });

            b.Entity<Attempt>()
                .Property(a => a.Score)
                .HasPrecision(18, 2);

            // ===== Responses (MCQ) =====
            b.Entity<Response>()
                .HasIndex(r => new { r.AttemptId, r.QuestionId })
                .IsUnique();

            // If Response has Score, keep precision; if not, remove this line.
            b.Entity<Response>()
                .Property(r => r.Score)
                .HasPrecision(18, 2);

            // ===== MCQ Bank =====
            b.Entity<McqQuestion>()
                .HasIndex(q => q.NormalizedText)
                .IsUnique()
                .HasFilter("[NormalizedText] IS NOT NULL");

            b.Entity<McqOption>()
                .HasIndex(o => new { o.QuestionId, o.IsCorrect });

            // Quiz ⇄ MCQ questions (map)
            b.Entity<QuizQuestion>()
                .HasIndex(qq => new { qq.QuizId, qq.QuestionId })
                .IsUnique();

            b.Entity<QuizQuestion>()
                .HasIndex(qq => new { qq.QuizId, qq.Order });

            // Attempt items ordering
            b.Entity<AttemptItem>()
                .HasIndex(ai => new { ai.AttemptId, ai.Order })
                .IsUnique();

            b.Entity<AttemptItem>()
                .HasIndex(ai => new { ai.AttemptId, ai.QuestionId })
                .IsUnique();

            // Decimal precision for MCQ weights
            b.Entity<McqQuestion>()
                .Property(q => q.Marks)
                .HasPrecision(18, 2);

            b.Entity<Quiz>()
                .Property(q => q.TotalMarks)
                .HasPrecision(18, 2);

            b.Entity<Quiz>()
                .Property(q => q.NegativeMarkPerWrong)
                .HasPrecision(18, 2);

            // ===== Coding Bank =====

            // CodeQuestion: decimals
            b.Entity<CodeQuestion>()
                .Property(q => q.MaxMarks)
                .HasPrecision(10, 2);

            // CodeTestCase: required relationship to parent + cascade delete
            b.Entity<CodeTestCase>()
                .HasKey(t => t.Id);

            b.Entity<CodeTestCase>()
                .Property(t => t.Weight)
                .HasDefaultValue(1);

            b.Entity<CodeTestCase>()
                .HasOne(t => t.CodeQuestion)
                .WithMany(q => q.TestCases)
                .HasForeignKey(t => t.CodeQuestionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Optional index to help fetch test cases per question
            b.Entity<CodeTestCase>()
                .HasIndex(t => new { t.CodeQuestionId, t.IsHidden });

            // AttemptCodeItem: one row per (Attempt, CodeQuestion)
            b.Entity<AttemptCodeItem>()
                .HasIndex(x => new { x.AttemptId, x.CodeQuestionId })
                .IsUnique();

            b.Entity<AttemptCodeItem>()
                .Property(a => a.MarksAwarded)
                .HasPrecision(10, 2);

            // Quiz ⇄ Coding questions (map)
            b.Entity<QuizCodingQuestion>()
                .HasKey(x => new { x.QuizId, x.CodeQuestionId });

            b.Entity<QuizCodingQuestion>()
                .HasOne(x => x.Quiz)
                .WithMany()                // no collection on Quiz for now
                .HasForeignKey(x => x.QuizId)
                .OnDelete(DeleteBehavior.Cascade);

            b.Entity<QuizCodingQuestion>()
                .HasOne(x => x.CodeQuestion)
                .WithMany()                // no collection on CodeQuestion for now
                .HasForeignKey(x => x.CodeQuestionId)
                .OnDelete(DeleteBehavior.Cascade);

            b.Entity<QuizCodingQuestion>()
                .HasIndex(x => new { x.QuizId, x.Order });
        }
    }
}
