using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Dalan.Relay.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "relay");

            migrationBuilder.CreateTable(
                name: "bots",
                schema: "relay",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    username = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    owner_user_id = table.Column<long>(type: "bigint", nullable: false),
                    token_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "memberships",
                schema: "relay",
                columns: table => new
                {
                    bot_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    state = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seq = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_memberships", x => new { x.bot_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_memberships_bots_bot_id",
                        column: x => x.bot_id,
                        principalSchema: "relay",
                        principalTable: "bots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "messages",
                schema: "relay",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    bot_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    seq = table.Column<long>(type: "bigint", nullable: false),
                    direction = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    delivery = table.Column<int>(type: "integer", nullable: true),
                    content = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_messages", x => x.id);
                    table.ForeignKey(
                        name: "fk_messages_memberships_bot_id_user_id",
                        columns: x => new { x.bot_id, x.user_id },
                        principalSchema: "relay",
                        principalTable: "memberships",
                        principalColumns: new[] { "bot_id", "user_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_messages_bot_id_user_id_seq",
                schema: "relay",
                table: "messages",
                columns: new[] { "bot_id", "user_id", "seq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_messages_pending",
                schema: "relay",
                table: "messages",
                column: "id",
                filter: "delivery = 0");

            // Unique without regard to letter case. EF Core cannot describe an index on an expression.
            migrationBuilder.Sql("CREATE UNIQUE INDEX ix_bots_username ON relay.bots (lower(username));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "messages",
                schema: "relay");

            migrationBuilder.DropTable(
                name: "memberships",
                schema: "relay");

            migrationBuilder.DropTable(
                name: "bots",
                schema: "relay");
        }
    }
}
