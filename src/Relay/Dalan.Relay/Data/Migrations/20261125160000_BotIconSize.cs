using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dalan.Relay.Data.Migrations
{
    /// <summary>
    /// REL-54: the real width and height of a bot's icon. Icons saved before stay null here; the manifest reads
    /// their header once and keeps the size (SQL cannot read an image header).
    /// </summary>
    [DbContext(typeof(RelayDb))]
    [Migration("20261125160000_BotIconSize")]
    public partial class BotIconSize : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "icon_width", schema: "relay", table: "bots", type: "integer", nullable: true);
            migrationBuilder.AddColumn<int>(name: "icon_height", schema: "relay", table: "bots", type: "integer", nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "icon_width", schema: "relay", table: "bots");
            migrationBuilder.DropColumn(name: "icon_height", schema: "relay", table: "bots");
        }
    }
}
