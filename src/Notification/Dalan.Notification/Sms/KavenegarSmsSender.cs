using Dalan.Notification.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dalan.Notification.Sms;

/// <summary>
/// NTF-02: sends the code with Kavenegar "verify lookup". The API key is part of the address,
/// so nothing here logs an address or an exception text.
/// </summary>
internal sealed partial class KavenegarSmsSender(
    HttpClient http, IOptionsMonitor<NotificationOptions> options, ILogger<KavenegarSmsSender> logger) : ISmsSender
{
    public async Task<bool> SendLoginCodeAsync(string phone, string code, CancellationToken cancellationToken)
    {
        var kavenegar = options.CurrentValue.Kavenegar;
        var address = new Uri($"{kavenegar.BaseUrl.TrimEnd('/')}/v1/{Uri.EscapeDataString(kavenegar.ApiKey)}/verify/lookup.json");
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            // Kavenegar wants the number as people write it in Iran: 09121234567.
            ["receptor"] = "0" + phone.Replace("+98", "", StringComparison.Ordinal),
            ["token"] = code,
            ["template"] = kavenegar.Template,
        });

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(kavenegar.TimeoutSeconds));
            using var response = await http.PostAsync(address, form, timeout.Token);
            if (response.IsSuccessStatusCode) return true;

            LogRefused(logger, (int)response.StatusCode);
            return false;
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LogDown(logger, exception.GetType().Name);
            return false;
        }
    }

    [LoggerMessage(LogLevel.Error, "Kavenegar refused the SMS with HTTP status {Status}. Check the API key, the template name and the account balance.")]
    private static partial void LogRefused(ILogger logger, int status);

    [LoggerMessage(LogLevel.Error, "Kavenegar could not be reached ({Reason}).")]
    private static partial void LogDown(ILogger logger, string reason);
}
