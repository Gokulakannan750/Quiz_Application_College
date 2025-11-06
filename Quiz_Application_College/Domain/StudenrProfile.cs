using System;

namespace Quiz_Application_College.Domain
{
    public class StudentProfile
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        // FK to AspNetUsers
        public string UserId { get; set; } = null!;

        // From Excel filename and sheet name
        public string College { get; set; } = "";
        public string Department { get; set; } = "";

        // From Excel columns
        public string RollNumber { get; set; } = "";
        public string Name { get; set; } = "";

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
