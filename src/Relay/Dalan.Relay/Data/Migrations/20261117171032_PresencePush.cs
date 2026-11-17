using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dalan.Relay.Data.Migrations
{
    /// <inheritdoc />
    public partial class PresencePush : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "last_read_seq",
                schema: "relay",
                table: "memberships",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_seen_at",
                schema: "relay",
                table: "bots",
                type: "timestamp with time zone",
                nullable: true);

            // REL-32: chats that exist already start as read.
            migrationBuilder.Sql("UPDATE relay.memberships SET last_read_seq = last_seq;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "last_read_seq",
                schema: "relay",
                table: "memberships");

            migrationBuilder.DropColumn(
                name: "last_seen_at",
                schema: "relay",
                table: "bots");
        }
    }
}
