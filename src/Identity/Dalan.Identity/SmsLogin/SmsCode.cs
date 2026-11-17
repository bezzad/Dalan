namespace Dalan.Identity.SmsLogin;

/// <summary>One SMS that was sent (AUTH-11). The rows of the last 24 hours are also the counters of the SMS limits.</summary>
public sealed class SmsCode
{
    public long Id { get; private set; }
    public required string Phone { get; set; }

    /// <summary>Keyed hash of the code. The code itself is never stored.</summary>
    public required byte[] CodeHash { get; set; }

    /// <summary>The client address that asked for it (SEC-08).</summary>
    public required string Ip { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public int Tries { get; set; }
    public bool Used { get; set; }
}
