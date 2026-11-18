using System.Net;
using System.Net.Sockets;
using System.Text;
using Dalan.Notification.Contracts;
using Dalan.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Dalan.Host.Tests;

/// <summary>NTF-10 and NTF-11. These tests need no database: the email sender does not use one.</summary>
public class EmailSenderTests
{
    private const string NoDatabase = "Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none;Timeout=2";
    private const string Password = "SMTP-SECRET-123";
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    /// <summary>Plays an SMTP server without TLS: answers every command and keeps the mail it got.</summary>
    private sealed class FakeSmtp : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);

        public FakeSmtp()
        {
            _listener.Start();
            Received = Task.Run(ServeOneAsync);
        }

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        /// <summary>The whole conversation the client wrote.</summary>
        public Task<string> Received { get; }

        private async Task<string> ServeOneAsync()
        {
            using var client = await _listener.AcceptTcpClientAsync();
            await using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII);
            await using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
            var all = new StringBuilder();
            await writer.WriteLineAsync("220 fake smtp");
            var inData = false;
            while (await reader.ReadLineAsync() is { } line)
            {
                all.AppendLine(line);
                if (inData)
                {
                    if (line != ".") continue;
                    inData = false;
                    await writer.WriteLineAsync("250 queued");
                }
                else if (line.StartsWith("DATA", StringComparison.OrdinalIgnoreCase))
                {
                    inData = true;
                    await writer.WriteLineAsync("354 go on");
                }
                else if (line.StartsWith("QUIT", StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteLineAsync("221 bye");
                    break;
                }
                else
                {
                    await writer.WriteLineAsync("250 ok");
                }
            }

            return all.ToString();
        }

        public void Dispose() => _listener.Stop();
    }

    private static DalanApp App(
        string provider, string environment = "Development", int port = 1, string host = "127.0.0.1", string logAddresses = "") => new(
        NoDatabase,
        new Dictionary<string, string>
        {
            ["Notification:EmailProvider"] = provider,
            ["Notification:LogEmailAddresses"] = logAddresses,
            ["Notification:Smtp:Host"] = host,
            ["Notification:Smtp:Port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Notification:Smtp:StartTls"] = "false",
            ["Notification:Smtp:UserName"] = "",
            ["Notification:Smtp:Password"] = Password,
            ["Notification:Smtp:From"] = "Dalan <no-reply@dalanet.ir>",
            ["Notification:Smtp:TimeoutSeconds"] = "5",
            ["Identity:DevLogin"] = "false",
        },
        web => web.UseEnvironment(environment))
    { RealEmail = true };

    private static Task<bool> SendAsync(DalanApp app, string email = "sara@example.com") =>
        app.Services.GetRequiredService<IEmailSender>().SendLoginCodeAsync(email, "123456", Cancel);

    [Fact]
    [Trait("Rule", "NTF-10")]
    public async Task NTF10_without_a_provider_every_send_fails()
    {
        await using var app = App(provider: "");

        Assert.False(await SendAsync(app));
        Assert.False(app.Services.GetRequiredService<IEmailSender>().IsOn);
    }

    [Fact]
    [Trait("Rule", "NTF-10")]
    public async Task NTF10_release_settings_have_no_email_provider()
    {
        await using var app = App(provider: "");
        var contentRoot = app.Services.GetRequiredService<IWebHostEnvironment>().ContentRootPath;

        var settings = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
            await File.ReadAllTextAsync(Path.Combine(contentRoot, "appsettings.json"), Cancel));

        Assert.Equal("", settings.GetProperty("Notification").GetProperty("EmailProvider").GetString());
    }

    [Fact]
    [Trait("Rule", "NTF-10")]
    public async Task NTF10_log_sender_on_a_server_writes_the_code_only_for_listed_addresses()
    {
        await using var app = App(provider: "log", environment: "Production", logAddresses: "omid@example.com, Sara@example.com");

        int CodeLines() => app.Logs.Lines.Count(line => line.Text.Contains("123456", StringComparison.Ordinal));

        Assert.True(await SendAsync(app, "sara@example.com"));
        Assert.Equal(1, CodeLines());

        Assert.False(await SendAsync(app, "other@example.com"));
        Assert.Equal(1, CodeLines());
    }

    [Theory]
    [Trait("Rule", "NTF-10")]
    [InlineData("log", "Production", "Notification:LogEmailAddresses")]
    [InlineData("pigeon", "Development", "pigeon")]
    public async Task NTF10_wrong_provider_setting_stops_the_start(string provider, string environment, string errorSays)
    {
        await using var app = App(provider, environment);

        var error = Assert.Throws<InvalidOperationException>(() => app.CreateClient());

        Assert.Contains("Notification:EmailProvider", error.Message, StringComparison.Ordinal);
        Assert.Contains(errorSays, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Rule", "NTF-11")]
    public async Task NTF11_smtp_server_gets_one_mail_with_the_code()
    {
        using var smtp = new FakeSmtp();
        await using var app = App("smtp", "Production", smtp.Port);

        var sent = await SendAsync(app);
        var received = await smtp.Received.WaitAsync(TimeSpan.FromSeconds(10), Cancel);

        Assert.True(sent);
        Assert.Contains("RCPT TO:<sara@example.com>", received, StringComparison.Ordinal);
        Assert.Contains("MAIL FROM:<no-reply@dalanet.ir>", received, StringComparison.Ordinal);
        Assert.Contains("123456", Body(received), StringComparison.Ordinal);
    }

    /// <summary>The text of the mail: the lines between the headers and the end, decoded from base64 (the body is Persian too).</summary>
    private static string Body(string conversation)
    {
        var data = conversation[(conversation.IndexOf("DATA", StringComparison.Ordinal) + 4)..];
        var lines = data.Split('\n').Skip(1).Select(line => line.TrimEnd('\r')).SkipWhile(line => line.Length > 0).TakeWhile(line => line != ".");
        return Encoding.UTF8.GetString(Convert.FromBase64String(string.Concat(lines)));
    }

    [Fact]
    [Trait("Rule", "NTF-11")]
    public async Task NTF11_server_down_is_a_failed_send_and_the_password_stays_out_of_the_log()
    {
        await using var app = App("smtp", "Production", port: 1);

        Assert.False(await SendAsync(app));
        Assert.DoesNotContain(app.Logs.Lines, line => line.Text.Contains(Password, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Rule", "NTF-11")]
    public async Task NTF11_missing_host_stops_the_start()
    {
        await using var app = App("smtp", "Production", host: "");

        var error = Assert.Throws<InvalidOperationException>(() => app.CreateClient());

        Assert.Contains("Notification:Smtp:Host", error.Message, StringComparison.Ordinal);
    }
}
