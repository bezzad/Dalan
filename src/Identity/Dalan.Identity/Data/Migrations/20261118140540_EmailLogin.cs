using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Dalan.Identity.Data.Migrations
{
    /// <inheritdoc />
    public partial class EmailLogin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "phone",
                schema: "identity",
                table: "users",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "email",
                schema: "identity",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "email_codes",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    email = table.Column<string>(type: "text", nullable: false),
                    code_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    ip = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tries = table.Column<int>(type: "integer", nullable: false),
                    used = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_codes", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                schema: "identity",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_phone_or_email",
                schema: "identity",
                table: "users",
                sql: "phone IS NOT NULL OR email IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_email_codes_created_at",
                schema: "identity",
                table: "email_codes",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_email_codes_email_created_at",
                schema: "identity",
                table: "email_codes",
                columns: new[] { "email", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_codes",
                schema: "identity");

            migrationBuilder.DropIndex(
                name: "ix_users_email",
                schema: "identity",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_phone_or_email",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "email",
                schema: "identity",
                table: "users");

            migrationBuilder.AlterColumn<string>(
                name: "phone",
                schema: "identity",
                table: "users",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
