using Quiz_Application_College.Data;
using Quiz_Application_College.Domain;
using Microsoft.EntityFrameworkCore;

namespace Quiz_Application_College.Data
{
    public static class DemoSeed
    {
        public static async Task SeedAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.MigrateAsync();

            if (!await db.Quizzes.AnyAsync())
            {
                var quiz = new Quiz { Title = "C Basics MCQ", Description = "Intro C", DurationMinutes = 30, TotalMarks = 100 };
                db.Quizzes.Add(quiz);
                await db.SaveChangesAsync();

                db.QuizSchedules.Add(new QuizSchedule
                {
                    QuizId = quiz.Id,
                    StartAt = DateTimeOffset.UtcNow.AddMinutes(10),
                    EndAt = DateTimeOffset.UtcNow.AddHours(1),
                    MaxAttempts = 1,
                    Timezone = "Asia/Kolkata"
                });
                await db.SaveChangesAsync();
            }
        }
    }
}
