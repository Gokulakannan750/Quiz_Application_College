using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quiz_Application_College.Data;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Quiz_Application_College.Areas.Student.Controllers
{
    [Area("Student")]
    [AllowAnonymous]
    [Route("Student/Auth")]
    public class AuthController : Controller
    {
        private readonly ApplicationDbContext _db;
        public AuthController(ApplicationDbContext db) => _db = db;

        // GET: /Student/Auth/Login
        [HttpGet("Login")]
        public IActionResult Login()
        {
            // If already logged in with the student cookie, go straight to dashboard
            var isStudent = User?.Identities?.Any(i =>
                i.IsAuthenticated && i.AuthenticationType == "StudentCookie") ?? false;

            if (isStudent)
                return RedirectToAction("Index", "Dashboard", new { area = "Student" });

            // No returnUrl logic at all – we always land on the main dashboard
            return View();
        }

        // POST: /Student/Auth/Login
        [HttpPost("Login")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password /* , string? returnUrl = null */)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError("", "Email and password are required.");
                return View();
            }

            var profile = await _db.StudentProfiles
                .FirstOrDefaultAsync(p => p.Email != null &&
                                          p.Email.ToLower() == email.Trim().ToLower() &&
                                          p.IsActive);

            if (profile == null || !VerifyPassword(password, profile.PasswordHash, profile.PasswordSalt))
            {
                ModelState.AddModelError("", "Invalid credentials.");
                return View();
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, profile.Id.ToString()),
                new Claim(ClaimTypes.Email, profile.Email ?? ""),
                new Claim(ClaimTypes.Name, profile.Name ?? (profile.RollNumber ?? "Student")),
                new Claim("spid", profile.Id.ToString())
            };

            var identity = new ClaimsIdentity(claims, "StudentCookie");
            await HttpContext.SignInAsync("StudentCookie", new ClaimsPrincipal(identity));

            // ALWAYS go to student dashboard after login
            return RedirectToAction("Index", "Dashboard", new { area = "Student" });
        }

        [HttpPost("Logout")]
        [Authorize(AuthenticationSchemes = "StudentCookie")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("StudentCookie");
            return RedirectToAction("Index", "Home", new { area = "" });
        }

        [HttpGet("Denied")]
        public IActionResult Denied() => Content("Access denied");

        private static bool VerifyPassword(string password, string hashBase64, string saltBase64)
        {
            if (string.IsNullOrWhiteSpace(saltBase64))
                return password == hashBase64; // fallback for plain-text dev passwords

            try
            {
                var salt = Convert.FromBase64String(saltBase64);
                using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256);
                var computed = pbkdf2.GetBytes(32);
                var expected = Convert.FromBase64String(hashBase64);
                return CryptographicOperations.FixedTimeEquals(computed, expected);
            }
            catch
            {
                return false;
            }
        }
    }
}
