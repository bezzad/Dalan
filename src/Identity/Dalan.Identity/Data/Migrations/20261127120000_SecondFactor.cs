using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dalan.Identity.Data.Migrations
{
    /// <summary>
    /// AUTH-37, SEC-15: the TOTP secret of a user (encrypted; on_at is null while the set-up waits for the first code),
    /// the hashed recovery codes, and the login tickets that wait for a code. Only raw SQL reads them, so the model
    /// snapshot does not change.
    /// </summary>
    [DbContext(typeof(IdentityDb))]
    [Migration("20261127120000_SecondFactor")]
    public partial class SecondFactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE identity.totp (
                    user_id bigint NOT NULL REFERENCES identity.users (id) ON DELETE CASCADE,
                    secret bytea NOT NULL,
                    on_at timestamp with time zone,
                    last_step bigint,
                    CONSTRAINT pk_totp PRIMARY KEY (user_id)
                );
                CREATE TABLE identity.recovery_codes (
                    user_id bigint NOT NULL REFERENCES identity.users (id) ON DELETE CASCADE,
                    code_hash bytea NOT NULL,
                    CONSTRAINT pk_recovery_codes PRIMARY KEY (user_id, code_hash)
                );
                CREATE TABLE identity.totp_tickets (
                    ticket_hash bytea NOT NULL,
                    user_id bigint NOT NULL REFERENCES identity.users (id) ON DELETE CASCADE,
                    method text NOT NULL,
                    expires_at timestamp with time zone NOT NULL,
                    tries integer NOT NULL,
                    CONSTRAINT pk_totp_tickets PRIMARY KEY (ticket_hash)
                );
                CREATE INDEX ix_totp_tickets_expires_at ON identity.totp_tickets (expires_at);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE identity.totp_tickets; DROP TABLE identity.recovery_codes; DROP TABLE identity.totp;");
        }
    }
}
