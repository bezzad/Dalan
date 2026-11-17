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

    /// <summary>Plays the provider: keeps each call and answers as the test says.</summary>
    private sealed class FakeProvider(HttpStatusCode status = HttpStatusCode.OK, bool down = false) : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Path, string Body)> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls.Add((request.Method, request.RequestUri!.AbsolutePath, await request.Content!.ReadAsStringAsync(cancellationToken)));
            if (down) throw new HttpRequestException("The provider cannot be reached.");
            return new HttpResponseMessage(status) { Content = new StringContent("""{"return":{"status":200}}""") };
        }
    }

    private static DalanApp App(string provider, string environment = "Development", FakeProvider? fake = null) => new(
        NoDatabase,
        new Dictionary<string, string>
        {
            ["Notification:SmsProvider"] = provider,
            ["Notification:Kavenegar:BaseUrl"] = "https://sms.test",
            ["Notification:Kavenegar:ApiKey"] = ApiKey,
            ["Notification:Kavenegar:Template"] = "dalan-login",
            ["Notification:Kavenegar:TimeoutSeconds"] = "10",
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

    private static Task<bool> SendAsync(DalanApp app) =>
        app.Services.GetRequiredService<ISmsSender>().SendLoginCodeAsync("+989121234567", "123456", Cancel);

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

    [Theory]
    [Trait("Rule", "NTF-01")]
    [InlineData("log", "Production", "Development")]
    [InlineData("pigeon", "Development", "pigeon")]
    public async Task NTF01_wrong_provider_setting_stops_the_start(string provider, string environment, string errorSays)
    {
        await using var app = App(provider, environment);

        var error = Assert.Throws<InvalidOperationException>(() => app.CreateClient());

        Assert.Contains("Notification:SmsProvider", error.Message, StringComparison.Ordinal);
        Assert.Contains(errorSays, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Rule", "NTF-02")]
    public async Task NTF02_kavenegar_gets_one_call_with_the_number_the_code_and_the_template()
    {
        var provider = new FakeProvider();
        await using var app = App("kavenegar", "Production", provider);

        var sent = await SendAsync(app);

        Assert.True(sent);
        var call = Assert.Single(provider.Calls);
        Assert.Equal(HttpMethod.Post, call.Method);
        Assert.Equal($"/v1/{ApiKey}/verify/lookup.json", call.Path);
        Assert.Equal("receptor=09121234567&token=123456&template=dalan-login", call.Body);
    }

    [Fact]
    [Trait("Rule", "NTF-02")]
    public async Task NTF02_error_status_is_a_failed_send()
    {
        await using var app = App("kavenegar", fake: new FakeProvider(HttpStatusCode.Forbidden));

        Assert.False(await SendAsync(app));
    }

    [Fact]
    [Trait("Rule", "NTF-02")]
    public async Task NTF02_network_error_is_a_failed_send()
    {
        await using var app = App("kavenegar", fake: new FakeProvider(down: true));

        Assert.False(await SendAsync(app));
    }

    [Fact]
    [Trait("Rule", "NTF-02")]
    public async Task NTF02_api_key_stays_out_of_the_log()
    {
        var down = new FakeProvider(down: true);
        await using var failing = App("kavenegar", fake: down);
        await using var working = App("kavenegar", fake: new FakeProvider());
        await using var refusing = App("kavenegar", fake: new FakeProvider(HttpStatusCode.Forbidden));

        await SendAsync(failing);
        await SendAsync(working);
        await SendAsync(refusing);

        Assert.Single(down.Calls);
        foreach (var app in new[] { failing, working, refusing })
            Assert.DoesNotContain(app.Logs.Lines, line => line.Text.Contains(ApiKey, StringComparison.Ordinal));
    }
}
