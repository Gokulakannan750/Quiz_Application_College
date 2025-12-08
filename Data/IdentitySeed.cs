using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Quiz_Application_College.Data
{
    public static class IdentitySeed
    {
        public static async Task SeedAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await ctx.Database.MigrateAsync();

            // 1) DECLARE ALL ROLES HERE
            string[] roles = new[] { "Admin", "Faculty", "Examiner", "Moderator", "Student" };

            foreach (var r in roles)
                if (!await roleMgr.RoleExistsAsync(r))
                    await roleMgr.CreateAsync(new IdentityRole(r));

            // 2) SEED SAMPLE USERS (OPTIONAL – for testing)
            // Admin
            await EnsureUserInRole(userMgr, "admin1@quiz.local", "Admin#12345", "Admin");

            // Other admin-like roles
            await EnsureUserInRole(userMgr, "faculty1@quiz.local", "Faculty#12345", "Faculty");
            await EnsureUserInRole(userMgr, "examiner1@quiz.local", "Examiner#12345", "Examiner");
            await EnsureUserInRole(userMgr, "moderator1@quiz.local", "Moderator#12345", "Moderator");

            // Student
            await EnsureUserInRole(userMgr, "student1@quiz.local", "Student#12345", "Student");
        }

        private static async Task EnsureUserInRole(UserManager<IdentityUser> userMgr, string email, string password, string role)
        {
            var user = await userMgr.FindByEmailAsync(email);
            if (user is null)
            {
                user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
                await userMgr.CreateAsync(user, password);
            }
            if (!await userMgr.IsInRoleAsync(user, role))
                await userMgr.AddToRoleAsync(user, role);
        }
    }
}
