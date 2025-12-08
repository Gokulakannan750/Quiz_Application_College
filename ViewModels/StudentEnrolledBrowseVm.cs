// File: ViewModels/StudentEnrolledBrowseVm.cs
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;

namespace Quiz_Application_College.ViewModels
{
    // Used by MCQ/Coding Enrollment -> FilterEnroll pages
    public class StudentEnrolledBrowseVm
    {
        // Filters
        public string? College { get; set; }
        public string? Department { get; set; }
        public string? Search { get; set; } // name / roll / email

        // Paging
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public int Total { get; set; }

        // Results
        public List<Row> Rows { get; set; } = new();

        // Dropdowns
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
        }
    }
}
