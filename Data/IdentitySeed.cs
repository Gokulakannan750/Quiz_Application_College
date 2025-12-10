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

            // Apply migrations once
            await ctx.Database.MigrateAsync();

            // Only these roles in production
            string[] roles = new[] { "Admin", "Trainer" };

            foreach (var r in roles)
            {
                if (!await roleMgr.RoleExistsAsync(r))
                {
                    await roleMgr.CreateAsync(new IdentityRole(r));
                }
            }

            // 1st real Admin – CHANGE THESE VALUES
            const string adminEmail = "admin@yourcollegequiz.com";
            const string adminPassword = "Admin@12345"; // temp strong password for first login

            await EnsureUserInRole(userMgr, adminEmail, adminPassword, "Admin");
        }

        private static async Task EnsureUserInRole(
            UserManager<IdentityUser> userMgr,
            string email,
            string password,
            string role)
        {
            var user = await userMgr.FindByEmailAsync(email);
            if (user is null)
            {
                user = new IdentityUser
                {
                    UserName = email,     // very important: username = email
                    Email = email,
                    EmailConfirmed = true
                };
                await userMgr.CreateAsync(user, password);
            }

            if (!await userMgr.IsInRoleAsync(user, role))
            {
                await userMgr.AddToRoleAsync(user, role);
            }
        }
    }
}
