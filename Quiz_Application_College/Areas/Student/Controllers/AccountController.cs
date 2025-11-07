using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;   // <-- IMPORTANT
using Quiz_Application_College.Data;
using Quiz_Application_College.Services.Security;

namespace Quiz_Application_College.Areas.Student.Controllers
{
    [Area("Student")]
    [AllowAnonymous]
    [Route("Student/Account")]
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _db;

        public AccountController(ApplicationDbContext db)
        {
            _db = db;
        }

        public class LoginVm
        {
            public string Email { get; set; } = "";
            public string Password { get; set; } = ""; // Roll number
            public bool RememberMe { get; set; }
            public string? ReturnUrl { get; set; }
        }

        [HttpGet("Login")]
        public IActionResult Login(string? returnUrl = null)
            => View("~/Areas/Student/Views/Account/Login.cshtml", new LoginVm { ReturnUrl = returnUrl });

        [HttpPost("Login")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginVm vm)
        {
            if (string.IsNullOrWhiteSpace(vm.Email) || string.IsNullOrWhiteSpace(vm.Password))
            {
                ModelState.AddModelError("", "Please enter Email and Roll Number.");
                return View("~/Areas/Student/Views/Account/Login.cshtml", vm);
            }

            var email = vm.Email.Trim().ToLower();
            var profile = await _db.StudentProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Email.ToLower() == email && p.IsActive);

            if (profile == null ||
                !StudentPasswordHasher.Verify(vm.Password.Trim(), profile.PasswordSalt, profile.PasswordHash))
            {
                ModelState.AddModelError("", "Invalid credentials.");
                return View("~/Areas/Student/Views/Account/Login.cshtml", vm);
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, profile.Id.ToString()),
                new Claim(ClaimTypes.Name, string.IsNullOrWhiteSpace(profile.Name) ? profile.Email : profile.Name),
                new Claim(ClaimTypes.Email, profile.Email),
                new Claim("RollNumber", profile.RollNumber ?? ""),
                new Claim(ClaimTypes.Role, "Student")
            };

            var identity = new ClaimsIdentity(claims, "StudentCookie");
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync("StudentCookie", principal, new AuthenticationProperties
            {
                IsPersistent = vm.RememberMe,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            });

            if (!string.IsNullOrWhiteSpace(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl))
                return Redirect(vm.ReturnUrl);

            return Redirect("/Student/Dashboard");
        }

        [HttpPost("Logout")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("StudentCookie");
            return Redirect("/Student/Account/Login");
        }
    }
}
