using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dalan.Testing;

namespace Dalan.Admin.Tests;

public class ContactTests(PostgresServer postgres)
{
    private static Task<HttpResponseMessage> SendAsync(HttpClient browser, string email, string text) =>
        browser.PostAsJsonAsync("/api/contact", new { email, text }, AdminApp.Cancel);

    private static async Task<JsonElement[]> MessagesAsync(HttpClient admin) => (await admin.JsonAsync("/api/admin/contact")).Rows();

    [Fact]
    [Trait("Rule", "ADM-17")]
    public async Task ADM17_visitor_sends_a_question()
    {
        await using var app = await AdminApp.StartAsync(postgres);
        var (admin, _) = await app.AdminAsync();
        var visitor = DalanApp.From(app.Browser(), "203.0.113.9:40000");

        var response = await SendAsync(visitor, "Ali@Example.com", "  How much is it?  ");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var message = Assert.Single(await MessagesAsync(admin));
        Assert.Equal("ali@example.com", message.GetProperty("email").GetString());
        Assert.Equal("How much is it?", message.GetProperty("text").GetString());
        Assert.Equal(JsonValueKind.Null, message.GetProperty("user_id").ValueKind);
        Assert.Equal("203.0.113.9", message.GetProperty("address").GetString());
        Assert.Equal(40000, message.GetProperty("port").GetInt32());
        Assert.False(message.GetProperty("read").GetBoolean());
        Assert.Equal(app.Time.GetUtcNow(), message.GetProperty("at").GetDateTimeOffset());
    }

    [Fact]
    [Trait("Rule", "ADM-17")]
    public async Task ADM17_visitor_without_email_gets_bad_email()
    {
        await using var app = await AdminApp.StartAsync(postgres);

        foreach (var email in new[] { "", "not-an-email" })
        {
            var response = await SendAsync(DalanApp.From(app.Browser(), "203.0.113.42:43000"), email, "Hi");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("bad_email", await response.ErrorCodeAsync());
        }
    }

    [Fact]
    [Trait("Rule", "ADM-17")]
    public async Task ADM17_user_with_email_gets_own_email_and_is_in_the_access_log()
    {
        await using var app = await AdminApp.StartAsync(postgres);
        var (admin, _) = await app.AdminAsync();
        var sara = DalanApp.From(await app.LoginByEmailAsync("sara@example.com"), "203.0.113.20:41000");
        var saraId = (await sara.JsonAsync("/api/me")).GetProperty("id").GetInt64();

        (await SendAsync(sara, "other@example.com", "Hi")).EnsureSuccessStatusCode();

        var message = Assert.Single(await MessagesAsync(admin));
        Assert.Equal("sara@example.com", message.GetProperty("email").GetString());
        Assert.Equal(saraId, message.GetProperty("user_id").GetInt64());
        Assert.Contains($"User {saraId} 203.0.113.20 41000", await app.AccessLogAsync());
    }

    [Fact]
    [Trait("Rule", "ADM-17")]
    public async Task ADM17_user_without_email_sends_the_typed_email()
    {
        await using var app = await AdminApp.StartAsync(postgres);
        var (admin, _) = await app.AdminAsync();
        var (browser, rezaId) = await app.LoginAsync("Reza");
        var reza = DalanApp.From(browser, "203.0.113.40:43000");

        Assert.Equal("bad_email", await (await SendAsync(reza, "", "Hi")).ErrorCodeAsync());
        (await SendAsync(reza, "reza@example.com", "Hi")).EnsureSuccessStatusCode();

        var message = Assert.Single(await MessagesAsync(admin));
        Assert.Equal("reza@example.com", message.GetProperty("email").GetString());
        Assert.Equal(rezaId, message.GetProperty("user_id").GetInt64());
    }

    [Fact]
    [Trait("Rule", "ADM-17")]
    public async Task ADM17_text_length_and_messages_per_address_are_limited()
    {
        await using var app = await AdminApp.StartAsync(postgres, ("Admin:ContactMaxLength", "10"), ("Admin:ContactPerAddressPerDay", "2"));
        var visitor = DalanApp.From(app.Browser(), "203.0.113.30:42000");

        var tooLong = await SendAsync(visitor, "ali@example.com", "12345678901");
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal("bad_text", await tooLong.ErrorCodeAsync());
        Assert.Equal("bad_text", await (await SendAsync(visitor, "ali@example.com", "   ")).ErrorCodeAsync());

        Assert.Equal(HttpStatusCode.Created, (await SendAsync(visitor, "ali@example.com", "1234567890")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await SendAsync(visitor, "ali@example.com", "Hi")).StatusCode);
        var third = await SendAsync(visitor, "ali@example.com", "Hi");
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        Assert.Equal("too_many_messages", await third.ErrorCodeAsync());

        // Another address is not held back, and a day later this one may send again.
        Assert.Equal(HttpStatusCode.Created, (await SendAsync(DalanApp.From(app.Browser(), "203.0.113.31:42000"), "b@example.com", "Hi")).StatusCode);
        app.Time.Advance(TimeSpan.FromHours(24));
        Assert.Equal(HttpStatusCode.Created, (await SendAsync(visitor, "ali@example.com", "Hi")).StatusCode);
    }

    [Fact]
    [Trait("Rule", "ADM-18")]
    public async Task ADM18_admin_marks_read_and_it_is_audited()
    {
        await using var app = await AdminApp.StartAsync(postgres);
        var (admin, adminId) = await app.AdminAsync();
        (await SendAsync(DalanApp.From(app.Browser(), "203.0.113.41:43000"), "ali@example.com", "First")).EnsureSuccessStatusCode();
        (await SendAsync(DalanApp.From(app.Browser(), "203.0.113.41:43001"), "ali@example.com", "Second")).EnsureSuccessStatusCode();
        var messages = await MessagesAsync(admin);
        Assert.Equal(["Second", "First"], messages.Select(m => m.GetProperty("text").GetString()));
        var id = messages[1].GetProperty("id").GetInt64();

        var marked = await admin.PostAsJsonAsync($"/api/admin/contact/{id}/read", new { read = true }, AdminApp.Cancel);

        Assert.True((await marked.JsonAsync()).GetProperty("read").GetBoolean());
        Assert.True((await MessagesAsync(admin))[1].GetProperty("read").GetBoolean());
        var older = await admin.JsonAsync($"/api/admin/contact?before={messages[0].GetProperty("id").GetInt64()}");
        Assert.Equal("First", Assert.Single(older.Rows()).GetProperty("text").GetString());
        var audit = (await admin.JsonAsync("/api/admin/audit")).Rows()[0];
        Assert.Equal(adminId, audit.GetProperty("user_id").GetInt64());
        Assert.Equal("contact.read", audit.GetProperty("action").GetString());
        Assert.Equal($"message {id}", audit.GetProperty("target").GetString());

        var missing = await admin.PostAsJsonAsync("/api/admin/contact/999999/read", new { read = true }, AdminApp.Cancel);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    [Trait("Rule", "ADM-18")]
    public async Task ADM18_only_the_admin_reads_messages()
    {
        await using var app = await AdminApp.StartAsync(postgres);
        var (user, _) = await app.LoginAsync();

        var response = await user.GetAsync("/api/admin/contact", AdminApp.Cancel);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("not_admin", await response.ErrorCodeAsync());
    }

    [Fact]
    [Trait("Rule", "ADM-17")]
    public async Task ADM17_max_length_is_in_the_public_settings()
    {
        await using var app = await AdminApp.StartAsync(postgres);

        var numbers = await app.CreateClient().JsonAsync("/api/public/settings");

        Assert.Equal(2000, numbers.GetProperty("contact_max_length").GetInt32());
    }
}
