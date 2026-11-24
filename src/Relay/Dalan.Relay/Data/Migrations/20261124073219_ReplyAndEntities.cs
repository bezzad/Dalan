using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dalan.Relay.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReplyAndEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // REL-52 and REL-53: ReplyTo and Entities are new fields of the jsonb content; no column changes. This keeps the model snapshot current.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
