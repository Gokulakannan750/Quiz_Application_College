using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quiz_Application_College.Migrations
{
    /// <inheritdoc />
    public partial class CodingQuestions_Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CodeQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Prompt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MaxMarks = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AllowedLanguagesCsv = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StarterCodeJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodeQuestions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AttemptCodeItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodeQuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PassedCount = table.Column<int>(type: "int", nullable: false),
                    TotalCount = table.Column<int>(type: "int", nullable: false),
                    MarksAwarded = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LastRunAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttemptCodeItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttemptCodeItems_Attempts_AttemptId",
                        column: x => x.AttemptId,
                        principalTable: "Attempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AttemptCodeItems_CodeQuestions_CodeQuestionId",
                        column: x => x.CodeQuestionId,
                        principalTable: "CodeQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CodeTestCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodeQuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Input = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExpectedOutput = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsHidden = table.Column<bool>(type: "bit", nullable: false),
                    Weight = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodeTestCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CodeTestCases_CodeQuestions_CodeQuestionId",
                        column: x => x.CodeQuestionId,
                        principalTable: "CodeQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttemptCodeItems_AttemptId_CodeQuestionId",
                table: "AttemptCodeItems",
                columns: new[] { "AttemptId", "CodeQuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttemptCodeItems_CodeQuestionId",
                table: "AttemptCodeItems",
                column: "CodeQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_CodeTestCases_CodeQuestionId",
                table: "CodeTestCases",
                column: "CodeQuestionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttemptCodeItems");

            migrationBuilder.DropTable(
                name: "CodeTestCases");

            migrationBuilder.DropTable(
                name: "CodeQuestions");
        }
    }
}
