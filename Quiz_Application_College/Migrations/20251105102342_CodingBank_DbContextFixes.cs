using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quiz_Application_College.Migrations
{
    /// <inheritdoc />
    public partial class CodingBank_DbContextFixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CodeTestCases_CodeQuestionId",
                table: "CodeTestCases");

            migrationBuilder.AlterColumn<int>(
                name: "Weight",
                table: "CodeTestCases",
                type: "int",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateIndex(
                name: "IX_CodeTestCases_CodeQuestionId_IsHidden",
                table: "CodeTestCases",
                columns: new[] { "CodeQuestionId", "IsHidden" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CodeTestCases_CodeQuestionId_IsHidden",
                table: "CodeTestCases");

            migrationBuilder.AlterColumn<int>(
                name: "Weight",
                table: "CodeTestCases",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_CodeTestCases_CodeQuestionId",
                table: "CodeTestCases",
                column: "CodeQuestionId");
        }
    }
}
