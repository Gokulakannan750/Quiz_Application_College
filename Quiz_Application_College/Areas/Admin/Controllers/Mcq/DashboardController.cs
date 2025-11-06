using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Quiz_Application_College.Areas.Admin.Controllers.Mcq
{
    [Area("Admin")]
    [Authorize(Policy = "IsAdmin")]
    [Route("Admin/MCQ/[controller]/[action]")]
    public class DashboardController : Controller
    {
        [HttpGet]
        [Route("/Admin/MCQ")] // pretty root url for MCQ dashboard
        public IActionResult Index()
        {
            // Render the MCQ Dashboard tiles
            return View("~/Areas/Admin/Views/Mcq/Dashboard/Index.cshtml");
        }
    }
}
