using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Quiz_Application_College.Areas.Admin.Controllers.Coding
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Trainer")]
    [Route("Admin/Coding/[controller]/[action]")]
    public class DashboardController : Controller
    {
        [HttpGet]
        [Route("/Admin/Coding")] // pretty root url for Coding dashboard
        public IActionResult Index()
        {
            return View("~/Areas/Admin/Views/Coding/Dashboard/Index.cshtml");
        }
    }
}
