using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Quiz_Application_College.Areas.Student.Controllers
{
    [Area("Student")]
    [Authorize(AuthenticationSchemes = "StudentCookie")]
    public class HomeController : Controller
    {
        // We don't actually need a separate Student Home; forward to the Student dashboard.
        [HttpGet("")]
        [HttpGet("Home")]
        [HttpGet("Index")]
        public IActionResult Index()
            => RedirectToAction("Index", "Dashboard", new { area = "Student" });
    }
}
