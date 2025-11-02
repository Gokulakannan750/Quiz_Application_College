using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Quiz_Application_College.Controllers
{
    [Authorize] // must be logged in to reach dashboard
    public class DashboardController : Controller
    {
        public IActionResult Index()
        {
            // Priority: Admin-like roles first, then Student
            if (User.IsInRole("Admin") || User.IsInRole("Faculty") || User.IsInRole("Examiner"))
            {
                return RedirectToAction("Index", "Home", new { area = "Admin" });
            }

            if (User.IsInRole("Student"))
            {
                return RedirectToAction("Index", "Home", new { area = "Student" });
            }

            // If user has no known role, send to access denied or a friendly page
            return RedirectToAction("AccessDenied", "Home");
        }
    }
}
