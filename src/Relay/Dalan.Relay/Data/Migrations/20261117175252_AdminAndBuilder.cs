using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dalan.Relay.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdminAndBuilder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "suspended_at",
                schema: "relay",
                table: "bots",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "suspended_at",
                schema: "relay",
                table: "bots");
        }
    }
}
