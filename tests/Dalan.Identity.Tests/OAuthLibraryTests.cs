using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using Dalan.Testing;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using static Dalan.Identity.Tests.Http;
using static Dalan.Identity.Tests.OAuth;

namespace Dalan.Identity.Tests;

/// <summary>
/// AUTH-42: a site that uses a real OpenID Connect library (Microsoft's, under ASP.NET Core's OpenIdConnect handler) logs in with
/// Dalan with no special code: the library reads the discovery and the keys itself and checks the ID token and the nonce.
/// </summary>
public class OAuthLibraryTests(PostgresServer postgres)
{
    /// <summary>The library fetches the issuer's addresses; in the test they go to the test server by path.</summary>
    private sealed class TestServerRetriever(HttpClient server) : IDocumentRetriever
    {
        public Task<string> GetDocumentAsync(string address, CancellationToken cancel) =>
            server.GetStringAsync(new Uri(address).PathAndQuery, cancel);
    }

    [Fact]
    [Trait("Rule", "AUTH-42")]
    public async Task AUTH42_a_real_openid_connect_library_logs_in_with_dalan()
    {
        await using var app = await StartSitesAsync(postgres);
        var (_, secret) = await ClientAsync(app);
        var (browser, _) = await app.LoginAsync("Sara");
        var manager = new ConfigurationManager<OpenIdConnectConfiguration>(
            $"{Issuer}/.well-known/openid-configuration", new OpenIdConnectConfigurationRetriever(), new TestServerRetriever(NoJar(app)));

        var config = await manager.GetConfigurationAsync(Cancel);
        var code = await CodeAsync(browser, Query(nonce: "nonce-lib"));
        var response = await TokenAsync(app, code, secret);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var message = new OpenIdConnectMessage(await response.Content.ReadAsStringAsync(Cancel));
        var principal = new JwtSecurityTokenHandler { MapInboundClaims = false }.ValidateToken(message.IdToken, new TokenValidationParameters
        {
            ValidIssuer = config.Issuer,
            ValidAudience = "shop",
            IssuerSigningKeys = config.SigningKeys,
            // The test clock is not the machine's clock.
            ValidateLifetime = false,
        }, out var token);
        var jwt = (JwtSecurityToken)token;
        // What the OpenIdConnect handler checks after the token step: the nonce it sent, the client, the times and at_hash.
        new OpenIdConnectProtocolValidator { RequireNonce = true, RequireTimeStampInNonce = false, RequireStateValidation = false }
            .ValidateTokenResponse(new OpenIdConnectProtocolValidationContext
            {
                ClientId = "shop",
                Nonce = "nonce-lib",
                ProtocolMessage = message,
                ValidatedIdToken = jwt,
            });

        Assert.Equal(Issuer, config.Issuer);
        Assert.Equal($"{Issuer}/oauth/token", config.TokenEndpoint);
        Assert.Contains("S256", config.CodeChallengeMethodsSupported);
        Assert.Equal("ES256", jwt.Header.Alg);
        Assert.Equal("Sara", principal.FindFirst("name")?.Value);
        Assert.False(string.IsNullOrEmpty(principal.FindFirst("sub")?.Value));
        Assert.Null(principal.FindFirst("phone_number"));
    }

    [Fact]
    [Trait("Rule", "AUTH-42")]
    [Trait("Rule", "AUTH-43")]
    public async Task AUTH42_with_no_key_in_the_settings_the_server_makes_one_in_its_file_and_keeps_it()
    {
        var file = Path.Combine(Path.GetTempPath(), $"dalan-oauth-{Guid.NewGuid():N}");
        try
        {
            string? first;
            await using (var app = await StartSitesAsync(postgres, ("Identity:OAuthKeyFile", file)))
                first = await KidAsync(app);
            await using var again = await StartSitesAsync(postgres, ("Identity:OAuthKeyFile", file));

            Assert.Equal(first, await KidAsync(again));
            if (!OperatingSystem.IsWindows())
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(file));
        }
        finally
        {
            File.Delete(file);
        }

        static async Task<string?> KidAsync(DalanApp app) =>
            (await NoJar(app).GetFromJsonAsync<System.Text.Json.JsonElement>("/oauth/jwks", Cancel)).GetProperty("keys")[0].GetProperty("kid").GetString();
    }
}
