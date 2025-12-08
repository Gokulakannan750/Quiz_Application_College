using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quiz_Application_College.Migrations
{
    /// <inheritdoc />
    public partial class AddAttemptAuditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StartIpAddress",
                table: "Attempts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StartUserAgent",
                table: "Attempts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubmitIpAddress",
                table: "Attempts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubmitUserAgent",
                table: "Attempts",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StartIpAddress",
                table: "Attempts");

            migrationBuilder.DropColumn(
                name: "StartUserAgent",
                table: "Attempts");

            migrationBuilder.DropColumn(
                name: "SubmitIpAddress",
                table: "Attempts");

            migrationBuilder.DropColumn(
                name: "SubmitUserAgent",
                table: "Attempts");
        }
    }
}
