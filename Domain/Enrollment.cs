using System;
using Quiz_Application_College.Domain;

namespace Quiz_Application_College.Domain
{
    public class Enrollment
    {
        public Guid Id { get; set; }

        public Guid QuizId { get; set; }
        public Quiz? Quiz { get; set; }

        // NEW: Strong FK to StudentProfiles (no Identity required)
        public Guid StudentProfileId { get; set; }
        public StudentProfile? StudentProfile { get; set; }

        // Legacy: keep for backward compatibility, now nullable
        public string? UserId { get; set; }

        public string Status { get; set; } = "Active";
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
