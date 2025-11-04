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

        /// <summary>
        /// Quizzes the student can still take right now (within an open schedule AND attempts remaining).
        /// </summary>
        public async Task<List<AvailableQuizItem>> GetAvailableAsync(string userId, DateTimeOffset now)
        {
            var list = await (from e in _db.Enrollments
                              join s in _db.QuizSchedules on e.QuizId equals s.QuizId
                              join q in _db.Quizzes on s.QuizId equals q.Id
                              where e.UserId == userId
                                    && e.Status == "Active"
                                    && q.IsPublished
                                    && s.StartAt <= now && now <= s.EndAt
                              select new AvailableQuizItem
                              {
                                  QuizId = q.Id,
                                  ScheduleId = s.Id,
                                  Title = q.Title,
                                  DurationMinutes = q.DurationMinutes,
                                  StartAt = s.StartAt,
                                  EndAt = s.EndAt,
                                  MaxAttempts = s.MaxAttempts,
                                  UsedAttempts = _db.Attempts.Count(a =>
                                      a.UserId == userId &&
                                      a.QuizId == q.Id &&
                                      a.StartedAt >= s.StartAt &&
                                      a.StartedAt <= s.EndAt)
                              })
                              .OrderBy(x => x.EndAt)
                              .ToListAsync();

            // Only show still-available ones
            return list.Where(x => x.UsedAttempts < x.MaxAttempts).ToList();
        }

        /// <summary>
        /// Open schedules the student cannot take because MaxAttempts is already reached.
        /// Useful for showing an info message.
        /// </summary>
        public async Task<List<AvailableQuizItem>> GetOpenButExhaustedAsync(string userId, DateTimeOffset now)
        {
            var list = await (from e in _db.Enrollments
                              join s in _db.QuizSchedules on e.QuizId equals s.QuizId
                              join q in _db.Quizzes on s.QuizId equals q.Id
                              where e.UserId == userId
                                    && e.Status == "Active"
                                    && q.IsPublished
                                    && s.StartAt <= now && now <= s.EndAt
                              select new AvailableQuizItem
                              {
                                  QuizId = q.Id,
                                  ScheduleId = s.Id,
                                  Title = q.Title,
                                  DurationMinutes = q.DurationMinutes,
                                  StartAt = s.StartAt,
                                  EndAt = s.EndAt,
                                  MaxAttempts = s.MaxAttempts,
                                  UsedAttempts = _db.Attempts.Count(a =>
                                      a.UserId == userId &&
                                      a.QuizId == q.Id &&
                                      a.StartedAt >= s.StartAt &&
                                      a.StartedAt <= s.EndAt)
                              })
                              .OrderBy(x => x.EndAt)
                              .ToListAsync();

            return list.Where(x => x.UsedAttempts >= x.MaxAttempts).ToList();
        }
    }
}
