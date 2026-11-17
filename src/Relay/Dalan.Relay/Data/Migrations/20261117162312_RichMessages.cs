using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dalan.Relay.Data.Migrations
{
    /// <inheritdoc />
    public partial class RichMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "deleted",
                schema: "relay",
                table: "messages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "edited_at",
                schema: "relay",
                table: "messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "pinned",
                schema: "relay",
                table: "messages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "version",
                schema: "relay",
                table: "messages",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "last_version",
                schema: "relay",
                table: "memberships",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "commands",
                schema: "relay",
                table: "bots",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "files",
                schema: "relay",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    bot_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    from_bot = table.Column<bool>(type: "boolean", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    content_type = table.Column<string>(type: "text", nullable: false),
                    size = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_files", x => x.id);
                });

            // REL-16: a message saved before versions keeps its place; the chat counts on from its newest number.
            migrationBuilder.Sql("UPDATE relay.messages SET version = seq;");
            migrationBuilder.Sql("UPDATE relay.memberships SET last_version = last_seq;");
            migrationBuilder.Sql("UPDATE relay.bots SET commands = '[]';");

            migrationBuilder.CreateIndex(
                name: "ix_messages_bot_id_user_id_version",
                schema: "relay",
                table: "messages",
                columns: new[] { "bot_id", "user_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_files_expires_at",
                schema: "relay",
                table: "files",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_files_user_id_created_at",
                schema: "relay",
                table: "files",
                columns: new[] { "user_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "files",
                schema: "relay");

            migrationBuilder.DropIndex(
                name: "ix_messages_bot_id_user_id_version",
                schema: "relay",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "deleted",
                schema: "relay",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "edited_at",
                schema: "relay",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "pinned",
                schema: "relay",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "version",
                schema: "relay",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "last_version",
                schema: "relay",
                table: "memberships");

            migrationBuilder.DropColumn(
                name: "commands",
                schema: "relay",
                table: "bots");
        }
    }
}
