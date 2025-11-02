using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quiz_Application_College.Services.Reports;

namespace Quiz_Application_College.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = "IsAdmin")]
    public class ReportsController : Controller
    {
        private readonly ExportService _export;
        public ReportsController(ExportService export) => _export = export;

        [HttpGet]
        public async Task<IActionResult> EnrollmentsXlsx()
        {
            var bytes = await _export.EnrollmentsXlsxAsync();
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "enrollments.xlsx");
        }

        [HttpGet]
        public async Task<IActionResult> AttemptsXlsx()
        {
            var bytes = await _export.AttemptsXlsxAsync();
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "attempts.xlsx");
        }
    }
}
