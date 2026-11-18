using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dalan.Identity.Data.Migrations
{
    /// <inheritdoc />
    public partial class GitHubLogin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "github_id",
                schema: "identity",
                table: "users",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_github_id",
                schema: "identity",
                table: "users",
                column: "github_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_users_github_id",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "github_id",
                schema: "identity",
                table: "users");
        }
    }
}
