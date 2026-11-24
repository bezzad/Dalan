namespace Dalan.Notification;

/// <summary>Section "Notification" of the settings. Starting values are in appsettings.json; nothing here is a default.</summary>
public sealed class NotificationOptions
{
    /// <summary>"smsir", "log" or empty: no SMS can be sent (NTF-01).</summary>
    public string? SmsProvider { get; set; }

    /// <summary>NTF-01: outside Development the "log" sender works only for these numbers (comma separated), for tests before a provider is ready.</summary>
    public string? LogSmsPhones { get; set; }

    public SmsIrOptions SmsIr { get; set; } = new();

    /// <summary>"smtp", "log" or empty: no email can be sent (NTF-10).</summary>
    public string? EmailProvider { get; set; }

    /// <summary>NTF-10: outside Development the "log" sender works only for these addresses (comma separated).</summary>
    public string? LogEmailAddresses { get; set; }

    public SmtpOptions Smtp { get; set; } = new();

    public PushOptions Push { get; set; } = new();
}

/// <summary>NTF-02: SMS.ir "verify" (fast send).</summary>
public sealed class SmsIrOptions
{
    public string BaseUrl { get; set; } = "";

    /// <summary>A secret: it lives only in the server's environment.</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>Number of the approved OTP template in the SMS.ir panel ("ارسال سریع").</summary>
    public int TemplateId { get; set; }

    /// <summary>Name of the code variable in the template text: CODE for #CODE#.</summary>
    public string CodeParameter { get; set; } = "";

    public int TimeoutSeconds { get; set; }
}

/// <summary>NTF-11: the SMTP server that sends the login emails.</summary>
public sealed class SmtpOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; }
    public bool StartTls { get; set; }
    public string UserName { get; set; } = "";

    /// <summary>A secret: it lives only in the server's environment.</summary>
    public string Password { get; set; } = "";

    /// <summary>The sender address, like "Dalan &lt;no-reply@dalanet.ir&gt;".</summary>
    public string From { get; set; } = "";

    public int TimeoutSeconds { get; set; }
}

/// <summary>Web Push (NTF-03 to NTF-09). Both keys empty = push is off.</summary>
public sealed class PushOptions
{
    /// <summary>VAPID public key, base64url (65 bytes). Made with scripts/vapid-keys.mjs.</summary>
    public string PublicKey { get; set; } = "";

    /// <summary>VAPID private key, base64url (32 bytes). A secret: it lives only in the server's environment.</summary>
    public string PrivateKey { get; set; } = "";

    /// <summary>How a push service can reach the owner: an https address or mailto:.</summary>
    public string Subject { get; set; } = "";

    /// <summary>Comma-separated hosts of the push services; their subdomains are allowed too (NTF-04).</summary>
    public string AllowedHosts { get; set; } = "";

    public int TtlSeconds { get; set; }
    public int MaxSubscriptionsPerUser { get; set; }
    public int MaxEndpointLength { get; set; }
    public int MaxBodyLength { get; set; }

    /// <summary>Read at start, like the number of workers.</summary>
    public int QueueSize { get; set; }
    public int Workers { get; set; }
    public int TimeoutSeconds { get; set; }
}
