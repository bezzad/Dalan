using System.Net;
using Dalan.Notification.Contracts;
using Dalan.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Dalan.Host.Tests;

/// <summary>NTF-01 and NTF-02. These tests need no database: the SMS sender does not use one.</summary>
public class SmsSenderTests
{
    private const string NoDatabase = "Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none;Timeout=2";
    private const string ApiKey = "SECRET-KEY-123";
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    /// <summary>Plays SMS.ir: keeps each call and answers as the test says.</summary>
    private sealed class FakeProvider(
        HttpStatusCode status = HttpStatusCode.OK, string body = """{"data":{"messageId":1,"cost":1},"status":1,"message":"ok"}""", bool down = false)
        : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Path, string? Key, string Body)> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls.Add((
                request.Method,
                request.RequestUri!.AbsolutePath,
                request.Headers.TryGetValues("x-api-key", out var keys) ? keys.Single() : null,
                await request.Content!.ReadAsStringAsync(cancellationToken)));
            if (down) throw new HttpRequestException("The provider cannot be reached.");
            return new HttpResponseMessage(status) { Content = new StringContent(body) };
        }
    }

    private static DalanApp App(string provider, string environment = "Development", FakeProvider? fake = null, string logSmsPhones = "") => new(
        NoDatabase,
        new Dictionary<string, string>
        {
            ["Notification:SmsProvider"] = provider,
            ["Notification:LogSmsPhones"] = logSmsPhones,
            ["Notification:SmsIr:BaseUrl"] = "https://sms.test",
            ["Notification:SmsIr:ApiKey"] = ApiKey,
            ["Notification:SmsIr:TemplateId"] = "123456",
            ["Notification:SmsIr:CodeParameter"] = "CODE",
            ["Notification:SmsIr:TimeoutSeconds"] = "10",
            ["Identity:DevLogin"] = "false",
        },
        web =>
        {
            web.UseEnvironment(environment);
            if (fake is not null)
            {
                web.ConfigureServices(services =>
                    services.ConfigureHttpClientDefaults(client => client.ConfigurePrimaryHttpMessageHandler(() => fake)));
            }
        })
    { RealSms = true };

    private static Task<bool> SendAsync(DalanApp app, string phone = "+989121234567") =>
        app.Services.GetRequiredService<ISmsSender>().SendLoginCodeAsync(phone, "123456", Cancel);

    [Fact]
    [Trait("Rule", "NTF-01")]
    public async Task NTF01_without_a_provider_every_send_fails()
    {
        await using var app = App(provider: "");

        Assert.False(await SendAsync(app));
    }

    [Fact]
    [Trait("Rule", "NTF-01")]
    public async Task NTF01_release_settings_have_no_log_sender()
    {
        await using var app = App(provider: "");
        var contentRoot = app.Services.GetRequiredService<IWebHostEnvironment>().ContentRootPath;

        var settings = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
            await File.ReadAllTextAsync(Path.Combine(contentRoot, "appsettings.json"), Cancel));

        Assert.Equal("", settings.GetProperty("Notification").GetProperty("SmsProvider").GetString());
    }

    [Fact]
    [Trait("Rule", "NTF-01")]
    public async Task NTF01_log_sender_in_development_writes_the_code_to_the_log()
    {
        await using var app = App(provider: "log");

        Assert.True(await SendAsync(app));
        Assert.Contains(app.Logs.Lines, line => line.Text.Contains("123456", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Rule", "NTF-01")]
    public async Task NTF01_log_sender_on_a_server_writes_the_code_only_for_listed_numbers()
    {
        await using var app = App(provider: "log", environment: "Production", logSmsPhones: "09351112233, 09121234567");

        int CodeLines() => app.Logs.Lines.Count(line => line.Text.Contains("123456", StringComparison.Ordinal));

        Assert.True(await SendAsync(app, "+989121234567"));
        Assert.Equal(1, CodeLines());

        Assert.False(await SendAsync(app, "+989129999999"));
        Assert.Equal(1, CodeLines());
    }

    [Theory]
    [Trait("Rule", "NTF-01")]
    [InlineData("log", "Production", "Notification:LogSmsPhones")]
    [InlineData("pigeon", "Development", "pigeon")]
    [InlineData("kavenegar", "Development", "kavenegar")]
    public async Task NTF01_wrong_provider_setting_stops_the_start(string provider, string environment, string errorSays)
    {
        await using var app = App(provider, environment);

        var error = Assert.Throws<InvalidOperationException>(() => app.CreateClient());

        Assert.Contains("Notification:SmsProvider", error.Message, StringComparison.Ordinal);
        Assert.Contains(errorSays, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Rule", "NTF-02")]
    public async Task NTF02_smsir_gets_one_verify_call_with_the_key_the_number_the_template_and_the_code()
    {
        var provider = new FakeProvider();
        await using var app = App("smsir", "Production", provider);

        var sent = await SendAsync(app);

        Assert.True(sent);
        var call = Assert.Single(provider.Calls);
        Assert.Equal(HttpMethod.Post, call.Method);
        Assert.Equal("/v1/send/verify", call.Path);
        Assert.Equal(ApiKey, call.Key);
        Assert.Equal("""{"mobile":"09121234567","templateId":123456,"parameters":[{"name":"CODE","value":"123456"}]}""", call.Body);
    }

    [Fact]
    [Trait("Rule", "NTF-02")]
    public async Task NTF02_refusal_is_a_failed_send_and_the_log_says_both_statuses()
    {
        await using var app = App("smsir", fake: new FakeProvider(HttpStatusCode.BadRequest, """{"data":null,"status":113,"message":"x"}"""));

        Assert.False(await SendAsync(app));
        Assert.Contains(app.Logs.Lines, line => line.Text.Contains("400", StringComparison.Ordinal) && line.Text.Contains("113", StringComparison.Ordinal));
    }

    [Theory]
    [Trait("Rule", "NTF-02")]
    [InlineData("""{"data":null,"status":0,"message":"x"}""")]
    [InlineData("<html>busy</html>")]
    [InlineData("")]
    public async Task NTF02_a_200_without_status_1_is_a_failed_send(string body)
    {
        await using var app = App("smsir", fake: new FakeProvider(HttpStatusCode.OK, body));

        Assert.False(await SendAsync(app));
    }

    [Fact]
    [Trait("Rule", "NTF-02")]
    public async Task NTF02_network_error_is_a_failed_send()
    {
        await using var app = App("smsir", fake: new FakeProvider(down: true));

        Assert.False(await SendAsync(app));
    }

    [Fact]
    [Trait("Rule", "NTF-02")]
    public async Task NTF02_api_key_stays_out_of_the_log()
    {
        var down = new FakeProvider(down: true);
        await using var failing = App("smsir", fake: down);
        await using var working = App("smsir", fake: new FakeProvider());
        await using var refusing = App("smsir", fake: new FakeProvider(HttpStatusCode.Unauthorized, """{"data":null,"status":10,"message":"x"}"""));

        await SendAsync(failing);
        await SendAsync(working);
        await SendAsync(refusing);

        Assert.Single(down.Calls);
        foreach (var app in new[] { failing, working, refusing })
            Assert.DoesNotContain(app.Logs.Lines, line => line.Text.Contains(ApiKey, StringComparison.Ordinal));
    }
}
