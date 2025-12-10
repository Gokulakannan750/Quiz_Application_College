using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Quiz_Application_College.Models;

namespace Quiz_Application_College.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }
        public IActionResult Index()
        {
            if (User?.Identity?.IsAuthenticated == true)
            {
                // Admin or Trainer ? Admin area dashboard
                if (User.IsInRole("Admin") || User.IsInRole("Trainer"))
                {
                    return RedirectToAction("Index", "Home", new { area = "Admin" });
                }

                // Student ? Student area dashboard
                if (User.IsInRole("Student"))
                {
                    return RedirectToAction("Index", "Home", new { area = "Student" });
                }
            }

            // Public landing page
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
