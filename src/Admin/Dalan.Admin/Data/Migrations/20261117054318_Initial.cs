using System;
using System.Net;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Dalan.Admin.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "admin");

            migrationBuilder.CreateTable(
                name: "access_log",
                schema: "admin",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    actor = table.Column<string>(type: "text", nullable: false),
                    actor_id = table.Column<long>(type: "bigint", nullable: false),
                    address = table.Column<IPAddress>(type: "inet", nullable: false),
                    port = table.Column<int>(type: "integer", nullable: true),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_access_log", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_access_log_actor_actor_id_at",
                schema: "admin",
                table: "access_log",
                columns: new[] { "actor", "actor_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_access_log_at",
                schema: "admin",
                table: "access_log",
                column: "at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "access_log",
                schema: "admin");
        }
    }
}
