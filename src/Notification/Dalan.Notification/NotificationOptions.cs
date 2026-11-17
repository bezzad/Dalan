namespace Dalan.Notification;

/// <summary>Section "Notification" of the settings. Starting values are in appsettings.json; nothing here is a default.</summary>
public sealed class NotificationOptions
{
    /// <summary>"kavenegar", "log" (Development only) or empty: no SMS can be sent (NTF-01).</summary>
    public string? SmsProvider { get; set; }

    public KavenegarOptions Kavenegar { get; set; } = new();
}

public sealed class KavenegarOptions
{
    public string BaseUrl { get; set; } = "";

    /// <summary>A secret: it lives only in the server's environment.</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>Name of the OTP template in the Kavenegar panel. It has one token: the code.</summary>
    public string Template { get; set; } = "";

    public int TimeoutSeconds { get; set; }
}
