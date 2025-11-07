using Microsoft.AspNetCore.Mvc.Rendering;
using System;

namespace Quiz_Application_College.ViewModels
{
    public class StudentBrowseVm
    {
        // Filters (read-only page)
        public string? College { get; set; }
        public string? Department { get; set; }
        public string? Search { get; set; } // name / roll / email

        // Paging
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public int Total { get; set; }

        // Results
        public System.Collections.Generic.List<Row> Rows { get; set; } = new();

        // Dropdown data
        public SelectList? CollegeOptions { get; set; }
        public SelectList? DepartmentOptions { get; set; }

        public class Row
        {
            public string UserId { get; set; } = "";
            public string College { get; set; } = "";
            public string Department { get; set; } = "";
            public string RollNumber { get; set; } = "";
            public string Name { get; set; } = "";
            public string Email { get; set; } = "";
            public DateTimeOffset CreatedAt { get; set; }

            // NEW: show enrollments per type
            public System.Collections.Generic.List<string> McqQuizzes { get; set; } = new();
            public System.Collections.Generic.List<string> CodingQuizzes { get; set; } = new();
        }
    }
}
