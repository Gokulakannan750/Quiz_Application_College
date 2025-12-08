using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quiz_Application_College.Migrations
{
    /// <inheritdoc />
    public partial class AddProgrammingLanguageToQuiz : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProgrammingLanguage",
                table: "Quizzes",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProgrammingLanguage",
                table: "Quizzes");
        }
    }
}
