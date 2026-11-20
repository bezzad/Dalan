using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dalan.Relay.Data.Migrations
{
    /// <inheritdoc />
    public partial class BotDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                schema: "relay",
                table: "bots",
                type: "timestamp with time zone",
                nullable: true);

            // REL-49: a deleted bot frees its username, so the unique index covers only bots that are not deleted.
            migrationBuilder.Sql("DROP INDEX relay.ix_bots_username;");
            migrationBuilder.Sql("CREATE UNIQUE INDEX ix_bots_username ON relay.bots (lower(username)) WHERE deleted_at IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX relay.ix_bots_username;");
            migrationBuilder.Sql("CREATE UNIQUE INDEX ix_bots_username ON relay.bots (lower(username));");
            migrationBuilder.DropColumn(
                name: "deleted_at",
                schema: "relay",
                table: "bots");
        }
    }
}
