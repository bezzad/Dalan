using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Dalan.Identity.Data.Migrations
{
    /// <inheritdoc />
    public partial class Login : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "password_hash",
                schema: "identity",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "sessions",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    token_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    previous_hash = table.Column<byte[]>(type: "bytea", nullable: true),
                    method = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    rotated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sessions", x => x.id);
                    table.ForeignKey(
                        name: "fk_sessions_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sms_codes",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    phone = table.Column<string>(type: "text", nullable: false),
                    code_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    ip = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tries = table.Column<int>(type: "integer", nullable: false),
                    used = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sms_codes", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sessions_previous_hash",
                schema: "identity",
                table: "sessions",
                column: "previous_hash");

            migrationBuilder.CreateIndex(
                name: "ix_sessions_token_hash",
                schema: "identity",
                table: "sessions",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sessions_user_id",
                schema: "identity",
                table: "sessions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_sms_codes_created_at",
                schema: "identity",
                table: "sms_codes",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_sms_codes_phone_created_at",
                schema: "identity",
                table: "sms_codes",
                columns: new[] { "phone", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sessions",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "sms_codes",
                schema: "identity");

            migrationBuilder.DropColumn(
                name: "password_hash",
                schema: "identity",
                table: "users");
        }
    }
}
