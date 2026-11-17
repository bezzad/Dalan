namespace Dalan.Identity;

/// <summary>Section "Identity" of the settings. Starting values are in appsettings.json; nothing here is a default.</summary>
public sealed class IdentityOptions
{
    /// <summary>Lifetime of a session token (AUTH-03).</summary>
    public int AccessTokenMinutes { get; set; }

    /// <summary>P-256 private key, PKCS#8, base64. A secret: it lives only in the server's environment (AUTH-07).</summary>
    public string? SigningKey { get; set; }

    /// <summary>Login without SMS. Works only in the Development environment (AUTH-06).</summary>
    public bool DevLogin { get; set; }

    public int MaxNameLength { get; set; }
}
