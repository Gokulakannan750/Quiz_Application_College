using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quiz_Application_College.Data;

namespace Quiz_Application_College.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = "IsAdmin")]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _db;
        public ReportsController(ApplicationDbContext db) => _db = db;

        // GET: /Admin/Reports
        public IActionResult Index() => View();

        // GET: /Admin/Reports/EnrollmentsXlsx
        [HttpGet]
        public async Task<IActionResult> EnrollmentsXlsx()
        {
            // join to AspNetUsers to fetch Email
            var rows = await (from e in _db.Enrollments
                              join q in _db.Quizzes on e.QuizId equals q.Id
                              join u in _db.Users on e.UserId equals u.Id
                              orderby q.Title, u.Email
                              select new
                              {
                                  Quiz = q.Title,
                                  UserEmail = u.Email,
                                  e.UserId,
                                  e.Status,
                                  e.CreatedAt
                              }).ToListAsync();

            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Enrollments");
            ws.Cell(1, 1).Value = "Quiz";
            ws.Cell(1, 2).Value = "User Email";
            ws.Cell(1, 3).Value = "User Id";
            ws.Cell(1, 4).Value = "Status";
            ws.Cell(1, 5).Value = "Enrolled At";

            var r = 2;
            foreach (var x in rows)
            {
                ws.Cell(r, 1).Value = x.Quiz;
                ws.Cell(r, 2).Value = x.UserEmail ?? "";
                ws.Cell(r, 3).Value = x.UserId;
                ws.Cell(r, 4).Value = x.Status;
                ws.Cell(r, 5).Value = x.CreatedAt.LocalDateTime; // DateTimeOffset -> DateTime
                r++;
            }

            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);

            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "Enrollments.xlsx");
        }

        // GET: /Admin/Reports/AttemptsXlsx
        [HttpGet]
        public async Task<IActionResult> AttemptsXlsx()
        {
            // join to AspNetUsers to fetch Email
            var rows = await (from a in _db.Attempts
                              join q in _db.Quizzes on a.QuizId equals q.Id
                              join u in _db.Users on a.UserId equals u.Id
                              orderby a.StartedAt descending
                              select new
                              {
                                  Quiz = q.Title,
                                  UserEmail = u.Email,
                                  a.UserId,
                                  a.StartedAt,
                                  a.SubmittedAt,
                                  a.Score
                              }).ToListAsync();

            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Attempts");
            ws.Cell(1, 1).Value = "Quiz";
            ws.Cell(1, 2).Value = "User Email";
            ws.Cell(1, 3).Value = "User Id";
            ws.Cell(1, 4).Value = "Started At";
            ws.Cell(1, 5).Value = "Submitted At";
            ws.Cell(1, 6).Value = "Score";

            var r = 2;
            foreach (var x in rows)
            {
                ws.Cell(r, 1).SetValue(x.Quiz);
                ws.Cell(r, 2).SetValue(x.UserEmail ?? "");
                ws.Cell(r, 3).SetValue(x.UserId);
                ws.Cell(r, 4).SetValue(x.StartedAt.LocalDateTime);

                // ✅ Don't assign null. Use DateTime if present, else empty string.
                if (x.SubmittedAt.HasValue)
                    ws.Cell(r, 5).SetValue(x.SubmittedAt.Value.LocalDateTime);
                else
                    ws.Cell(r, 5).SetValue(string.Empty);

                ws.Cell(r, 6).SetValue(x.Score);
                r++;
            }

            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);

            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "Attempts.xlsx");
        }

    }
}
