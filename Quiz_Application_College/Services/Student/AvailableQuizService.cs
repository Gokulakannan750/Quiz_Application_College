using Microsoft.EntityFrameworkCore;
using Quiz_Application_College.Data;

namespace Quiz_Application_College.Services.Student
{
    public class AvailableQuizItem
    {
        public Guid QuizId { get; set; }
        public Guid ScheduleId { get; set; }
        public string Title { get; set; } = default!;
        public DateTimeOffset StartAt { get; set; }
        public DateTimeOffset EndAt { get; set; }
        public int DurationMinutes { get; set; }

        public int MaxAttempts { get; set; }
        public int UsedAttempts { get; set; }
        public int RemainingAttempts => Math.Max(0, MaxAttempts - UsedAttempts);
    }

    public class AvailableQuizService
    {
        private readonly ApplicationDbContext _db;
        public AvailableQuizService(ApplicationDbContext db) => _db = db;

        public async Task<List<AvailableQuizItem>> GetAvailableAsync(string userId, DateTimeOffset now)
        {
            // 1) Get open schedules for quizzes the user is enrolled in
            var open = await (from e in _db.Enrollments
                              join q in _db.Quizzes on e.QuizId equals q.Id
                              join s in _db.QuizSchedules on q.Id equals s.QuizId
                              where e.UserId == userId
                                    && e.Status == "Active"
                                    && q.IsPublished
                                    && s.StartAt <= now && now <= s.EndAt
                              select new
                              {
                                  QuizId = q.Id,
                                  ScheduleId = s.Id,
                                  Title = q.Title,
                                  DurationMinutes = q.DurationMinutes,
                                  StartAt = s.StartAt,
                                  EndAt = s.EndAt,
                                  MaxAttempts = s.MaxAttempts
                              })
                              .ToListAsync();

            var result = new List<AvailableQuizItem>(open.Count);

            // 2) For each open schedule window, count attempts started within the window
            foreach (var o in open)
            {
                var used = await _db.Attempts
                    .Where(a => a.UserId == userId
                             && a.QuizId == o.QuizId
                             && a.StartedAt >= o.StartAt
                             && a.StartedAt <= o.EndAt)
                    .CountAsync();

                result.Add(new AvailableQuizItem
                {
                    QuizId = o.QuizId,
                    ScheduleId = o.ScheduleId,
                    Title = o.Title,
                    StartAt = o.StartAt,
                    EndAt = o.EndAt,
                    DurationMinutes = o.DurationMinutes,
                    MaxAttempts = o.MaxAttempts,
                    UsedAttempts = used
                });
            }

            return result.OrderBy(x => x.EndAt).ToList();
        }

    }
}
