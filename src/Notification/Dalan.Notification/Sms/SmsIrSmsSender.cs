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

    public Task<bool> SendLoginCodeAsync(string phone, string code, CancellationToken cancellationToken)
    {
        var smsIr = options.CurrentValue.SmsIr;
        return SendAsync(phone, smsIr.TemplateId, smsIr.CodeParameter, code, cancellationToken);
    }

    /// <summary>NTF-13: the same call with the notice template; template 0 means no notice SMS.</summary>
    public Task<bool> SendNoticeAsync(string phone, string text, CancellationToken cancellationToken)
    {
        var smsIr = options.CurrentValue.SmsIr;
        if (smsIr.NoticeTemplateId == 0)
        {
            LogNoNoticeTemplate(logger);
            return Task.FromResult(false);
        }

        return SendAsync(phone, smsIr.NoticeTemplateId, smsIr.NoticeParameter, text, cancellationToken);
    }

    private async Task<bool> SendAsync(string phone, int templateId, string parameter, string value, CancellationToken cancellationToken)
    {
        var smsIr = options.CurrentValue.SmsIr;
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"{smsIr.BaseUrl.TrimEnd('/')}/v1/send/verify"))
        {
            Content = JsonContent.Create(
                new VerifyRequest(
                    // As people write it in Iran: 09121234567. SMS.ir takes 9121234567 too.
                    "0" + phone.Replace("+98", "", StringComparison.Ordinal),
                    templateId,
                    [new VerifyParameter(parameter, value)]),
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

    [LoggerMessage(LogLevel.Warning, "A notice SMS was not sent: Notification:SmsIr:NoticeTemplateId is 0.")]
    private static partial void LogNoNoticeTemplate(ILogger logger);
}
