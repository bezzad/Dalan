using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dalan.Identity.Data.Migrations
{
    /// <summary>
    /// AUTH-39: once, every user with a verified phone becomes a builder (2 = approved) from "none" (0) or "requested" (1).
    /// "refused" (3) by an admin stays. Only data changes, so the model snapshot does not.
    /// </summary>
    [DbContext(typeof(IdentityDb))]
    [Migration("20261128090000_BuilderByPhone")]
    public partial class BuilderByPhone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE identity.users SET builder = 2 WHERE builder IN (0, 1) AND phone IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
