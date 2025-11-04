using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quiz_Application_College.Migrations
{
    /// <inheritdoc />
    public partial class McqQuestion_Unique_By_NormalizedText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NormalizedText",
                table: "McqQuestions",
                type: "nvarchar(1024)",
                nullable: true);

            // Fill existing rows: lowercase + collapse all whitespace
            migrationBuilder.Sql(@"
        UPDATE q SET NormalizedText =
            LOWER(
                REPLACE(
                    REPLACE(
                        REPLACE(
                            REPLACE(q.[Text], CHAR(13), ' ')
                        , CHAR(10), ' ')
                    , CHAR(9), ' ')
                , '  ', ' ')
            )
        FROM McqQuestions q
    ");

            // collapse multiple spaces repeatedly (simple double-pass)
            migrationBuilder.Sql(@"
        UPDATE McqQuestions SET NormalizedText = REPLACE(NormalizedText, '  ', ' ');
        UPDATE McqQuestions SET NormalizedText = REPLACE(NormalizedText, '  ', ' ');
    ");

            migrationBuilder.CreateIndex(
                name: "IX_McqQuestions_NormalizedText",
                table: "McqQuestions",
                column: "NormalizedText",
                unique: true,
                filter: "[NormalizedText] IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_McqQuestions_NormalizedText",
                table: "McqQuestions");

            migrationBuilder.DropColumn(
                name: "NormalizedText",
                table: "McqQuestions");
        }

    }
}
