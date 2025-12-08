using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quiz_Application_College.Migrations
{
    /// <inheritdoc />
    public partial class AddMcqQuizFolder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "McqQuizFolderId",
                table: "Quizzes",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "McqQuizFolders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OrderNo = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_McqQuizFolders", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Quizzes_McqQuizFolderId",
                table: "Quizzes",
                column: "McqQuizFolderId");

            migrationBuilder.AddForeignKey(
                name: "FK_Quizzes_McqQuizFolders_McqQuizFolderId",
                table: "Quizzes",
                column: "McqQuizFolderId",
                principalTable: "McqQuizFolders",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Quizzes_McqQuizFolders_McqQuizFolderId",
                table: "Quizzes");

            migrationBuilder.DropTable(
                name: "McqQuizFolders");

            migrationBuilder.DropIndex(
                name: "IX_Quizzes_McqQuizFolderId",
                table: "Quizzes");

            migrationBuilder.DropColumn(
                name: "McqQuizFolderId",
                table: "Quizzes");
        }
    }
}
