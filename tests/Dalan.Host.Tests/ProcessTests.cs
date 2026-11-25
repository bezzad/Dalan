using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;

namespace Dalan.Host.Tests;

/// <summary>Starts the real server as its own process, the way systemd does.</summary>
public class ProcessTests
{
    private sealed record Health(string Status, string Version, string Commit);

    [Fact]
    [Trait("Rule", "OPS-04")]
    public async Task OPS04_log_lines_on_stdout_are_json()
    {
        using var server = await RunningServer.Start(new());

        using var line = JsonDocument.Parse(server.FirstLogLine);
        foreach (var key in new[] { "Timestamp", "LogLevel", "Category", "Message" })
            Assert.True(line.RootElement.TryGetProperty(key, out _), $"log line has no {key}: {server.FirstLogLine}");
    }

    [Fact]
    [Trait("Rule", "OPS-05")]
    public async Task OPS05_environment_variable_wins_over_settings_file()
    {
        using var server = await RunningServer.Start(new() { ["Dalan__Version"] = "env" });

        var health = await server.Client.GetFromJsonAsync<Health>("/health", TestContext.Current.CancellationToken);

        Assert.Equal("env", health?.Version);
    }

    private sealed class RunningServer : IDisposable
    {
        private readonly Process _process;

        private RunningServer(Process process, HttpClient client, string firstLogLine)
        {
            _process = process;
            Client = client;
            FirstLogLine = firstLogLine;
        }

        public HttpClient Client { get; }
        public string FirstLogLine { get; }

        public static async Task<RunningServer> Start(Dictionary<string, string> env)
        {
            var port = FreePort();
            var hostDir = HostOutputDirectory();
            var start = new ProcessStartInfo("dotnet", [Path.Combine(hostDir, "Dalan.Host.dll")])
            {
                RedirectStandardOutput = true,
                WorkingDirectory = hostDir,
            };
            start.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}";
            foreach (var (key, value) in env) start.Environment[key] = value;

            var process = Process.Start(start)!;
            var firstLine = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30)) ?? "";
            var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
            await WaitUntilUp(client);
            return new RunningServer(process, client, firstLine);
        }

        // tests/Dalan.Host.Tests/bin/<config>/<tfm>/ -> src/Dalan.Host/bin/<config>/<tfm>/
        private static string HostOutputDirectory() =>
            AppContext.BaseDirectory.Replace(
                Path.Combine("tests", "Dalan.Host.Tests"), Path.Combine("src", "Dalan.Host"), StringComparison.Ordinal);

        private static async Task WaitUntilUp(HttpClient client)
        {
            for (var i = 0; i < 100; i++)
            {
                try
                {
                    if ((await client.GetAsync("/health")).StatusCode == HttpStatusCode.OK) return;
                }
                catch (HttpRequestException)
                {
                }
                await Task.Delay(100);
            }
            throw new TimeoutException("Server did not start.");
        }

        private static int FreePort()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }

        public void Dispose()
        {
            Client.Dispose();
            _process.Kill(entireProcessTree: true);
            _process.WaitForExit();
            _process.Dispose();
        }
    }
}
