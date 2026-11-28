using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dalan.Identity.Data.Migrations
{
    /// <summary>
    /// AUTH-40 to AUTH-43, "Login with Dalan": the sites' clients (only the hash of the secret), the users' consents (with the
    /// site's owner at that time, so a new owner of the name starts with none), the one-use codes and the access tokens (hashes).
    /// Only raw SQL reads them, so the model snapshot does not change.
    /// </summary>
    [DbContext(typeof(IdentityDb))]
    [Migration("20261128120000_LoginWithDalan")]
    public partial class LoginWithDalan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE identity.oauth_clients (
                    client_id text NOT NULL,
                    owner_user_id bigint NOT NULL REFERENCES identity.users (id) ON DELETE CASCADE,
                    secret_hash bytea NOT NULL,
                    created_at timestamp with time zone NOT NULL,
                    CONSTRAINT pk_oauth_clients PRIMARY KEY (client_id)
                );
                CREATE INDEX ix_oauth_clients_owner_user_id ON identity.oauth_clients (owner_user_id);
                CREATE TABLE identity.oauth_consents (
                    user_id bigint NOT NULL REFERENCES identity.users (id) ON DELETE CASCADE,
                    client_id text NOT NULL,
                    owner_user_id bigint NOT NULL,
                    scopes text NOT NULL,
                    email boolean NOT NULL,
                    created_at timestamp with time zone NOT NULL,
                    CONSTRAINT pk_oauth_consents PRIMARY KEY (user_id, client_id)
                );
                CREATE TABLE identity.oauth_codes (
                    code_hash bytea NOT NULL,
                    client_id text NOT NULL,
                    user_id bigint NOT NULL REFERENCES identity.users (id) ON DELETE CASCADE,
                    redirect_uri text NOT NULL,
                    challenge text NOT NULL,
                    nonce text,
                    scopes text NOT NULL,
                    email boolean NOT NULL,
                    proxy boolean NOT NULL,
                    expires_at timestamp with time zone NOT NULL,
                    used_at timestamp with time zone,
                    CONSTRAINT pk_oauth_codes PRIMARY KEY (code_hash)
                );
                CREATE INDEX ix_oauth_codes_expires_at ON identity.oauth_codes (expires_at);
                CREATE TABLE identity.oauth_tokens (
                    token_hash bytea NOT NULL,
                    code_hash bytea NOT NULL,
                    client_id text NOT NULL,
                    user_id bigint NOT NULL REFERENCES identity.users (id) ON DELETE CASCADE,
                    scopes text NOT NULL,
                    email boolean NOT NULL,
                    expires_at timestamp with time zone NOT NULL,
                    CONSTRAINT pk_oauth_tokens PRIMARY KEY (token_hash)
                );
                CREATE INDEX ix_oauth_tokens_expires_at ON identity.oauth_tokens (expires_at);
                CREATE INDEX ix_oauth_tokens_code_hash ON identity.oauth_tokens (code_hash);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TABLE identity.oauth_tokens; DROP TABLE identity.oauth_codes;
                DROP TABLE identity.oauth_consents; DROP TABLE identity.oauth_clients;
                """);
        }
    }
}
