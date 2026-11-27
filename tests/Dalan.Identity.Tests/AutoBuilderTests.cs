using System.Net.Http.Json;
using Dalan.Testing;
using static Dalan.Identity.Tests.Http;

namespace Dalan.Identity.Tests;

/// <summary>AUTH-39: a verified phone makes a builder without the admin.</summary>
public class AutoBuilderTests(PostgresServer postgres)
{
    private const string Phone = "09121234567";

    private Task<DalanApp> StartAsync(bool on = true) => Http.StartAsync(postgres, ("Identity:AutoBuilder", on ? "true" : "false"));

    private static async Task<string?> BuilderAsync(HttpClient browser) =>
        (await JsonAsync(await browser.GetAsync("/api/me", Cancel))).GetProperty("builder").GetString();

    [Fact]
    [Trait("Rule", "AUTH-39")]
    public async Task AUTH39_a_new_sms_user_is_a_builder()
    {
        await using var app = await StartAsync();

        var browser = await app.LoginBySmsAsync(Phone);

        Assert.Equal("approved", await BuilderAsync(browser));
    }

    [Fact]
    [Trait("Rule", "AUTH-39")]
    public async Task AUTH39_an_added_phone_makes_a_builder()
    {
        await using var app = await StartAsync();
        var browser = await app.LoginByEmailAsync("sara@example.com");
        (await DalanApp.AskCodeAsync(browser, Phone)).EnsureSuccessStatusCode();

        (await browser.PostAsJsonAsync("/api/account/phone", new { phone = Phone, code = app.Sms.Sent[^1].Code }, Cancel))
            .EnsureSuccessStatusCode();

        Assert.Equal("approved", await BuilderAsync(browser));
    }

    [Fact]
    [Trait("Rule", "AUTH-39")]
    public async Task AUTH39_refused_stays_refused()
    {
        await using var app = await StartAsync();
        await app.AddUserAsync(phone: "+989121234567");
        await PostgresServer.ExecuteAsync(app.ConnectionString, "UPDATE identity.users SET builder = 3");

        var browser = await app.LoginBySmsAsync(Phone);

        Assert.Equal("refused", await BuilderAsync(browser));
    }

    [Fact]
    [Trait("Rule", "AUTH-39")]
    public async Task AUTH39_off_leaves_the_admin_to_decide()
    {
        await using var app = await StartAsync(on: false);

        var browser = await app.LoginBySmsAsync(Phone);

        Assert.Equal("none", await BuilderAsync(browser));
    }
}
