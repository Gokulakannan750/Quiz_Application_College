using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Quiz_Application_College.Data;

namespace Quiz_Application_College.Services.Reports
{
    public class ExportService
    {
        private readonly ApplicationDbContext _db;
        public ExportService(ApplicationDbContext db) => _db = db;

        public async Task<byte[]> EnrollmentsXlsxAsync()
        {
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Enrollments");

            // header
            ws.Cell(1, 1).Value = "Id";
            ws.Cell(1, 2).Value = "QuizTitle";
            ws.Cell(1, 3).Value = "UserId";
            ws.Cell(1, 4).Value = "Email";
            ws.Cell(1, 5).Value = "Status";
            ws.Cell(1, 6).Value = "CreatedAt";

            var q = await _db.Enrollments
                .Include(e => e.Quiz)
                .OrderByDescending(e => e.CreatedAt)
                .Select(e => new {
                    e.Id,
                    QuizTitle = e.Quiz!.Title,
                    e.UserId,
                    Email = _db.Users.Where(u => u.Id == e.UserId).Select(u => u.Email).FirstOrDefault(),
                    e.Status,
                    e.CreatedAt
                }).ToListAsync();

            int r = 2;
            foreach (var x in q)
            {
                ws.Cell(r, 1).Value = x.Id.ToString();
                ws.Cell(r, 2).Value = x.QuizTitle;
                ws.Cell(r, 3).Value = x.UserId;
                ws.Cell(r, 4).Value = x.Email;
                ws.Cell(r, 5).Value = x.Status;
                ws.Cell(r, 6).Value = x.CreatedAt.LocalDateTime;
                r++;
            }

            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        public async Task<byte[]> AttemptsXlsxAsync()
        {
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Attempts");

            // header
            ws.Cell(1, 1).Value = "AttemptId";
            ws.Cell(1, 2).Value = "QuizTitle";
            ws.Cell(1, 3).Value = "UserId";
            ws.Cell(1, 4).Value = "Email";
            ws.Cell(1, 5).Value = "StartedAt";
            ws.Cell(1, 6).Value = "SubmittedAt";
            ws.Cell(1, 7).Value = "Score";

            var q = await _db.Attempts
                .Include(a => a.Quiz)
                .OrderByDescending(a => a.StartedAt)
                .Select(a => new {
                    a.Id,
                    QuizTitle = a.Quiz!.Title,
                    a.UserId,
                    Email = _db.Users.Where(u => u.Id == a.UserId).Select(u => u.Email).FirstOrDefault(),
                    a.StartedAt,
                    a.SubmittedAt,
                    a.Score
                }).ToListAsync();

            int r = 2;
            foreach (var x in q)
            {
                ws.Cell(r, 1).Value = x.Id.ToString();
                ws.Cell(r, 2).Value = x.QuizTitle;
                ws.Cell(r, 3).Value = x.UserId;
                ws.Cell(r, 4).Value = x.Email;
                ws.Cell(r, 5).Value = x.StartedAt.LocalDateTime;
                ws.Cell(r, 6).Value = x.SubmittedAt?.LocalDateTime;
                ws.Cell(r, 7).Value = x.Score;
                r++;
            }

            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }
    }
}
