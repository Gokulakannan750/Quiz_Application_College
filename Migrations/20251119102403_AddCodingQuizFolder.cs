using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quiz_Application_College.Migrations
{
    /// <inheritdoc />
    public partial class AddCodingQuizFolder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CodingQuizFolderId",
                table: "Quizzes",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "McqQuizFolders",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "CodingQuizFolders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OrderNo = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodingQuizFolders", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Quizzes_CodingQuizFolderId",
                table: "Quizzes",
                column: "CodingQuizFolderId");

            migrationBuilder.AddForeignKey(
                name: "FK_Quizzes_CodingQuizFolders_CodingQuizFolderId",
                table: "Quizzes",
                column: "CodingQuizFolderId",
                principalTable: "CodingQuizFolders",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Quizzes_CodingQuizFolders_CodingQuizFolderId",
                table: "Quizzes");

            migrationBuilder.DropTable(
                name: "CodingQuizFolders");

            migrationBuilder.DropIndex(
                name: "IX_Quizzes_CodingQuizFolderId",
                table: "Quizzes");

            migrationBuilder.DropColumn(
                name: "CodingQuizFolderId",
                table: "Quizzes");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "McqQuizFolders",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);
        }
    }
}
