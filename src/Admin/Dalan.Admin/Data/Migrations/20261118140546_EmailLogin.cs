using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dalan.Admin.Data.Migrations
{
    /// <inheritdoc />
    public partial class EmailLogin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "email",
                schema: "admin",
                table: "access_log",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_access_log_email",
                schema: "admin",
                table: "access_log",
                column: "email",
                filter: "email IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_access_log_email",
                schema: "admin",
                table: "access_log");

            migrationBuilder.DropColumn(
                name: "email",
                schema: "admin",
                table: "access_log");
        }
    }
}
