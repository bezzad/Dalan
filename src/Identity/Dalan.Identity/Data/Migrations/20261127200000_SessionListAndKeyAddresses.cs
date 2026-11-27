using System.Net;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dalan.Identity.Data.Migrations
{
    /// <summary>
    /// AUTH-35: each session keeps its browser, system and last address for the session list.
    /// AUTH-38: each access key keeps the addresses it works from (empty: any address).
    /// </summary>
    [DbContext(typeof(IdentityDb))]
    [Migration("20261127200000_SessionListAndKeyAddresses")]
    public partial class SessionListAndKeyAddresses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<IPAddress>(
                name: "address",
                schema: "identity",
                table: "sessions",
                type: "inet",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "browser",
                schema: "identity",
                table: "sessions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "os",
                schema: "identity",
                table: "sessions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string[]>(
                name: "addresses",
                schema: "identity",
                table: "access_keys",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "address", schema: "identity", table: "sessions");
            migrationBuilder.DropColumn(name: "browser", schema: "identity", table: "sessions");
            migrationBuilder.DropColumn(name: "os", schema: "identity", table: "sessions");
            migrationBuilder.DropColumn(name: "addresses", schema: "identity", table: "access_keys");
        }
    }
}
