using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dalan.Relay.Data.Migrations
{
    /// <inheritdoc />
    public partial class BotIcon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "duration",
                schema: "relay",
                table: "files",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "icon",
                schema: "relay",
                table: "bots",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "icon_type",
                schema: "relay",
                table: "bots",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "icon_version",
                schema: "relay",
                table: "bots",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // REL-41, REL-51: the showcase icon becomes the bot's icon. Only a listed entry's icon: it was public already.
            // A pending or refused icon was seen only by the owner and the admin, and must not become public here.
            migrationBuilder.Sql("""
                UPDATE relay.bots b
                SET icon = e.icon, icon_type = e.icon_type, icon_version = 1
                FROM relay.showcase e
                WHERE e.bot_id = b.id AND e.icon IS NOT NULL AND e.state = 1 -- ShowcaseState.Listed
                """);

            migrationBuilder.DropColumn(
                name: "icon",
                schema: "relay",
                table: "showcase");

            migrationBuilder.DropColumn(
                name: "icon_type",
                schema: "relay",
                table: "showcase");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "icon",
                schema: "relay",
                table: "showcase",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "icon_type",
                schema: "relay",
                table: "showcase",
                type: "text",
                nullable: true);

            // Going back loses the icons of bots with no showcase entry: the old schema had no place for them.
            migrationBuilder.Sql("""
                UPDATE relay.showcase e
                SET icon = b.icon, icon_type = b.icon_type
                FROM relay.bots b
                WHERE e.bot_id = b.id AND b.icon IS NOT NULL
                """);

            migrationBuilder.DropColumn(
                name: "duration",
                schema: "relay",
                table: "files");

            migrationBuilder.DropColumn(
                name: "icon",
                schema: "relay",
                table: "bots");

            migrationBuilder.DropColumn(
                name: "icon_type",
                schema: "relay",
                table: "bots");

            migrationBuilder.DropColumn(
                name: "icon_version",
                schema: "relay",
                table: "bots");

        }
    }
}
