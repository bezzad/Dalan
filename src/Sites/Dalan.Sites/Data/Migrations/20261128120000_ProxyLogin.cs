using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dalan.Sites.Data.Migrations
{
    /// <summary>
    /// SITE-26: the sites whose proxy asks for Dalan's login. A row goes with its site. Only raw SQL reads it, so the model
    /// snapshot does not change.
    /// </summary>
    [DbContext(typeof(SitesDb))]
    [Migration("20261128120000_ProxyLogin")]
    public partial class ProxyLogin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE sites.proxy_logins (
                    site_id bigint NOT NULL REFERENCES sites.sites (id) ON DELETE CASCADE,
                    created_at timestamp with time zone NOT NULL,
                    CONSTRAINT pk_proxy_logins PRIMARY KEY (site_id)
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE sites.proxy_logins;");
        }
    }
}
