using Quiz_Application_College.Data;
using Microsoft.EntityFrameworkCore;

namespace Quiz_Application_College.Data
{
    public static class DemoSeed
    {
        public static async Task SeedAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // In production, we only make sure the database exists and migrations are applied.
            await db.Database.MigrateAsync();

            // No demo quizzes or schedules here.
            // All quizzes / schedules will be created manually from the admin dashboard.
        }
    }
}
