using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quiz_Application_College.Migrations
{
    /// <inheritdoc />
    public partial class Quiz_Coding_Link : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QuizCodingQuestions",
                columns: table => new
                {
                    QuizId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodeQuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuizCodingQuestions", x => new { x.QuizId, x.CodeQuestionId });
                    table.ForeignKey(
                        name: "FK_QuizCodingQuestions_CodeQuestions_CodeQuestionId",
                        column: x => x.CodeQuestionId,
                        principalTable: "CodeQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QuizCodingQuestions_Quizzes_QuizId",
                        column: x => x.QuizId,
                        principalTable: "Quizzes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuizCodingQuestions_CodeQuestionId",
                table: "QuizCodingQuestions",
                column: "CodeQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuizCodingQuestions_QuizId_Order",
                table: "QuizCodingQuestions",
                columns: new[] { "QuizId", "Order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuizCodingQuestions");
        }
    }
}
