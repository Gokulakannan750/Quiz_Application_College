using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Quiz_Application_College.Areas.Student.Controllers
{
    [Area("Student")]
    [Authorize(AuthenticationSchemes = "StudentCookie")]
    [Route("Student")]
    public class DashboardController : Controller
    {
        [HttpGet("")]
        [HttpGet("Dashboard")]
        public IActionResult Index() => View();
    }
}
