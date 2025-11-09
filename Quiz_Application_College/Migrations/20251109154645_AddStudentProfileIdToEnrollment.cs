using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quiz_Application_College.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentProfileIdToEnrollment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Add column nullable first (so we can backfill)
            migrationBuilder.AddColumn<Guid>(
                name: "StudentProfileId",
                table: "Enrollments",
                type: "uniqueidentifier",
                nullable: true);

            // 2) Make UserId nullable (legacy)
            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "Enrollments",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            // 3) Backfill StudentProfileId by matching legacy UserId -> StudentProfiles.UserId
            migrationBuilder.Sql(@"
        UPDATE e
        SET e.StudentProfileId = p.Id
        FROM Enrollments e
        INNER JOIN StudentProfiles p ON p.UserId = e.UserId
        WHERE e.StudentProfileId IS NULL AND e.UserId IS NOT NULL;
    ");

            // 4) If any NULLs remain, you can either stop or map them to a dummy row.
            //    The following block assigns any remaining NULLs to a safe placeholder profile:
            migrationBuilder.Sql(@"
        IF EXISTS (SELECT 1 FROM Enrollments WHERE StudentProfileId IS NULL)
        BEGIN
            DECLARE @DummyId UNIQUEIDENTIFIER = NEWID();
            IF NOT EXISTS (SELECT 1 FROM StudentProfiles WHERE RollNumber = 'UNKNOWN')
            BEGIN
                INSERT INTO StudentProfiles (Id, College, Department, RollNumber, Name, CreatedAt, IsActive)
                VALUES (@DummyId, 'Unknown', 'Unknown', 'UNKNOWN', 'Unknown', SYSUTCDATETIME(), 0)
            END
            ELSE
            BEGIN
                SELECT @DummyId = Id FROM StudentProfiles WHERE RollNumber = 'UNKNOWN'
            END

            UPDATE Enrollments SET StudentProfileId = @DummyId WHERE StudentProfileId IS NULL;
        END
    ");

            // 5) Add FK & indexes
            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_StudentProfileId",
                table: "Enrollments",
                column: "StudentProfileId");

            migrationBuilder.AddForeignKey(
                name: "FK_Enrollments_StudentProfiles_StudentProfileId",
                table: "Enrollments",
                column: "StudentProfileId",
                principalTable: "StudentProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_QuizId_StudentProfileId",
                table: "Enrollments",
                columns: new[] { "QuizId", "StudentProfileId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Enrollments_StudentProfiles_StudentProfileId",
                table: "Enrollments");

            migrationBuilder.DropIndex(
                name: "IX_Enrollments_StudentProfileId",
                table: "Enrollments");

            migrationBuilder.DropIndex(
                name: "IX_Enrollments_QuizId_StudentProfileId",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "StudentProfileId",
                table: "Enrollments");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "Enrollments",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);
        }
    }
}
