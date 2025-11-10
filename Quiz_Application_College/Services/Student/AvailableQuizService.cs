using Microsoft.EntityFrameworkCore;
using Quiz_Application_College.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Quiz_Application_College.Services.Student
{
    public class AvailableQuizItem
    {
        public Guid QuizId { get; set; }
        public Guid ScheduleId { get; set; }
        public string Title { get; set; } = "";
        public int DurationMinutes { get; set; }
        public DateTimeOffset StartAt { get; set; }
        public DateTimeOffset EndAt { get; set; }
        public int MaxAttempts { get; set; }
        public int UsedAttempts { get; set; }
    }

    public interface IAvailableQuizService
    {
        Task<List<AvailableQuizItem>> GetAvailableAsync(string userId, DateTimeOffset nowUtc);
        Task<List<AvailableQuizItem>> GetOpenButExhaustedAsync(string userId, DateTimeOffset nowUtc);
    }

    public class AvailableQuizService : IAvailableQuizService
    {
        private readonly ApplicationDbContext _db;
        public AvailableQuizService(ApplicationDbContext db) => _db = db;

        // MAIN LIST: show quizzes where the student is enrolled AND schedule is currently active
        public async Task<List<AvailableQuizItem>> GetAvailableAsync(string userId, DateTimeOffset nowUtc)
        {
            // 1) resolve StudentProfile.Id for this user
            var profileId = await _db.StudentProfiles
                .Where(p => p.UserId == userId)
                .Select(p => p.Id)
                .FirstOrDefaultAsync();

            if (profileId == Guid.Empty)
                return new List<AvailableQuizItem>();

            // 2) query by StudentProfileId; treat NULL status as Active
            var q = from e in _db.Enrollments
                    join s in _db.QuizSchedules on e.QuizId equals s.QuizId
                    join quiz in _db.Quizzes on s.QuizId equals quiz.Id
                    where e.StudentProfileId == profileId
                          && (e.Status == "Active" || e.Status == null)
                          && s.StartAt <= nowUtc && nowUtc <= s.EndAt
                    select new AvailableQuizItem
                    {
                        QuizId = quiz.Id,
                        ScheduleId = s.Id,
                        Title = quiz.Title,
                        DurationMinutes = quiz.DurationMinutes,
                        StartAt = s.StartAt,
                        EndAt = s.EndAt,
                        MaxAttempts = s.MaxAttempts,
                        UsedAttempts = _db.Attempts.Count(a =>
                            a.UserId == userId &&
                            a.QuizId == quiz.Id &&
                            a.StartedAt >= s.StartAt &&
                            a.StartedAt <= s.EndAt)
                    };

            var list = await q.OrderBy(x => x.EndAt).ToListAsync();

            // 3) filter out items where attempts are exhausted
            return list.Where(x => x.UsedAttempts < x.MaxAttempts).ToList();
        }

        // SECONDARY LIST: open but already exhausted (if you show a note)
        public async Task<List<AvailableQuizItem>> GetOpenButExhaustedAsync(string userId, DateTimeOffset nowUtc)
        {
            var profileId = await _db.StudentProfiles
                .Where(p => p.UserId == userId)
                .Select(p => p.Id)
                .FirstOrDefaultAsync();

            if (profileId == Guid.Empty)
                return new List<AvailableQuizItem>();

            var q = from e in _db.Enrollments
                    join s in _db.QuizSchedules on e.QuizId equals s.QuizId
                    join quiz in _db.Quizzes on s.QuizId equals quiz.Id
                    where e.StudentProfileId == profileId
                          && (e.Status == "Active" || e.Status == null)
                          && s.StartAt <= nowUtc && nowUtc <= s.EndAt
                    select new AvailableQuizItem
                    {
                        QuizId = quiz.Id,
                        ScheduleId = s.Id,
                        Title = quiz.Title,
                        DurationMinutes = quiz.DurationMinutes,
                        StartAt = s.StartAt,
                        EndAt = s.EndAt,
                        MaxAttempts = s.MaxAttempts,
                        UsedAttempts = _db.Attempts.Count(a =>
                            a.UserId == userId &&
                            a.QuizId == quiz.Id &&
                            a.StartedAt >= s.StartAt &&
                            a.StartedAt <= s.EndAt)
                    };

            var list = await q.OrderBy(x => x.EndAt).ToListAsync();
            return list.Where(x => x.UsedAttempts >= x.MaxAttempts).ToList();
        }
    }
}
