using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quiz_Application_College.Migrations
{
    /// <inheritdoc />
    public partial class SwapEnrollmentUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
        IF EXISTS (SELECT 1 FROM sys.indexes 
                   WHERE name = 'IX_Enrollments_QuizId_UserId' 
                     AND object_id = OBJECT_ID('dbo.Enrollments'))
        BEGIN DROP INDEX [IX_Enrollments_QuizId_UserId] ON [dbo].[Enrollments]; END
    ");
            migrationBuilder.Sql(@"
        IF NOT EXISTS (SELECT 1 FROM sys.indexes 
                       WHERE name = 'IX_Enrollments_QuizId_StudentProfileId' 
                         AND object_id = OBJECT_ID('dbo.Enrollments'))
        BEGIN
            CREATE UNIQUE INDEX [IX_Enrollments_QuizId_StudentProfileId]
            ON [dbo].[Enrollments]([QuizId],[StudentProfileId]);
        END
    ");
        }
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
        IF EXISTS (SELECT 1 FROM sys.indexes 
                   WHERE name = 'IX_Enrollments_QuizId_StudentProfileId' 
                     AND object_id = OBJECT_ID('dbo.Enrollments'))
        BEGIN DROP INDEX [IX_Enrollments_QuizId_StudentProfileId] ON [dbo].[Enrollments]; END
    ");
            migrationBuilder.Sql(@"
        IF NOT EXISTS (SELECT 1 FROM sys.indexes 
                       WHERE name = 'IX_Enrollments_QuizId_UserId' 
                         AND object_id = OBJECT_ID('dbo.Enrollments'))
        BEGIN
            CREATE UNIQUE INDEX [IX_Enrollments_QuizId_UserId]
            ON [dbo].[Enrollments]([QuizId],[UserId]);
        END
    ");
        }

    }
}
