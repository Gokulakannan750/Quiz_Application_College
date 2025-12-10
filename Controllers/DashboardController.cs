using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Quiz_Application_College.Controllers
{
    [Authorize] 
    public class DashboardController : Controller
    {
        public IActionResult Index()
        {
            // Priority: Admin-like roles first, then Student
            if (User.IsInRole("Admin") || User.IsInRole("Trainer"))
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
