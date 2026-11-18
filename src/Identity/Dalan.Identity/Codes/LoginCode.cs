namespace Dalan.Identity.Codes;

/// <summary>
/// One login code that was sent, by SMS or by email (AUTH-11, AUTH-24).
/// The rows of the last 24 hours are also the counters of the limits.
/// </summary>
public abstract class LoginCode
{
    public long Id { get; private set; }

    /// <summary>The phone number or the email address the code went to.</summary>
    public string Target { get; set; } = "";

    /// <summary>Keyed hash of the code. The code itself is never stored.</summary>
    public byte[] CodeHash { get; set; } = [];

    /// <summary>The client address that asked for it (SEC-08, SEC-13).</summary>
    public string Ip { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }
    public int Tries { get; set; }
    public bool Used { get; set; }
}

/// <summary>A code sent by SMS. Its <see cref="LoginCode.Target"/> is the column "phone".</summary>
public sealed class SmsCode : LoginCode;

/// <summary>A code sent by email. Its <see cref="LoginCode.Target"/> is the column "email".</summary>
public sealed class EmailCode : LoginCode;
