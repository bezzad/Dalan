using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Dalan.Notification.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dalan.Notification.Sms;

/// <summary>
/// NTF-02: sends the code with SMS.ir "verify" (fast send with an approved template). The API key goes in a header,
/// and nothing here logs a header or an exception text.
/// </summary>
internal sealed partial class SmsIrSmsSender(
    HttpClient http, IOptionsMonitor<NotificationOptions> options, ILogger<SmsIrSmsSender> logger) : ISmsSender
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<bool> SendLoginCodeAsync(string phone, string code, CancellationToken cancellationToken)
    {
        var smsIr = options.CurrentValue.SmsIr;
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"{smsIr.BaseUrl.TrimEnd('/')}/v1/send/verify"))
        {
            Content = JsonContent.Create(
                new VerifyRequest(
                    // As people write it in Iran: 09121234567. SMS.ir takes 9121234567 too.
                    "0" + phone.Replace("+98", "", StringComparison.Ordinal),
                    smsIr.TemplateId,
                    [new VerifyParameter(smsIr.CodeParameter, code)]),
                options: Json),
        };
        request.Headers.Add("x-api-key", smsIr.ApiKey);

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(smsIr.TimeoutSeconds));
            using var response = await http.SendAsync(request, timeout.Token);
            var status = await StatusAsync(response, timeout.Token);
            if (response.IsSuccessStatusCode && status == 1) return true;

            LogRefused(logger, (int)response.StatusCode, status?.ToString(CultureInfo.InvariantCulture) ?? "none");
            return false;
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LogDown(logger, exception.GetType().Name);
            return false;
        }
    }

    /// <summary>The "status" number of the answer; 1 is success. Null when the answer is not the JSON SMS.ir sends.</summary>
    private static async Task<int?> StatusAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var answer = await response.Content.ReadFromJsonAsync<VerifyAnswer>(Json, cancellationToken);
            return answer?.Status;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record VerifyRequest(string Mobile, int TemplateId, VerifyParameter[] Parameters);

    private sealed record VerifyParameter(string Name, string Value);

    private sealed record VerifyAnswer(int? Status);

    [LoggerMessage(LogLevel.Error, "SMS.ir refused the SMS: HTTP status {Status}, SMS.ir status {SmsIrStatus}. Check the API key, the template ID and the account balance (https://app.sms.ir/developer/help/statusCode).")]
    private static partial void LogRefused(ILogger logger, int status, string smsIrStatus);

    [LoggerMessage(LogLevel.Error, "SMS.ir could not be reached ({Reason}).")]
    private static partial void LogDown(ILogger logger, string reason);
}
