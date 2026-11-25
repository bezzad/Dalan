using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dalan.Relay.Data.Migrations
{
    /// <summary>REL-56: where the chats of a merged account went. Only raw SQL reads it, so the model snapshot does not change.</summary>
    [DbContext(typeof(RelayDb))]
    [Migration("20261125120000_MovedChats")]
    public partial class MovedChats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE relay.moved_chats (
                    bot_id bigint NOT NULL,
                    from_user_id bigint NOT NULL,
                    to_user_id bigint NOT NULL,
                    moved_at timestamp with time zone NOT NULL,
                    announced_at timestamp with time zone,
                    CONSTRAINT pk_moved_chats PRIMARY KEY (bot_id, from_user_id)
                );
                CREATE INDEX ix_moved_chats_to_user_id ON relay.moved_chats (to_user_id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE relay.moved_chats;");
        }
    }
}
