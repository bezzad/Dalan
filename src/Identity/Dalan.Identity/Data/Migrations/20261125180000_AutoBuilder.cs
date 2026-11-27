using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dalan.Identity.Data.Migrations
{
    /// <summary>
    /// AUTH-39: the builder requests open when the auto builder came are approved once (2 = approved, 1 = requested).
    /// The new state 3 (refused) is a value of the same column, so the model snapshot does not change.
    /// </summary>
    [DbContext(typeof(IdentityDb))]
    [Migration("20261125180000_AutoBuilder")]
    public partial class AutoBuilder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE identity.users SET builder = 2 WHERE builder = 1 AND phone IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
