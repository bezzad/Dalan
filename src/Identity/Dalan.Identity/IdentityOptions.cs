namespace Dalan.Identity;

/// <summary>Section "Identity" of the settings. Starting values are in appsettings.json; nothing here is a default.</summary>
public sealed class IdentityOptions
{
    /// <summary>Lifetime of a session token (AUTH-03).</summary>
    public int AccessTokenMinutes { get; set; }

    /// <summary>Lifetime of a refresh token, counted from its last use (AUTH-08, AUTH-09).</summary>
    public int RefreshTokenDays { get; set; }

    /// <summary>An old refresh token that comes back inside this time is only refused; later it ends the session (AUTH-10).</summary>
    public int RefreshReuseGraceSeconds { get; set; }

    /// <summary>P-256 private key, PKCS#8, base64. A secret: it lives only in the server's environment (AUTH-07).</summary>
    public string? SigningKey { get; set; }

    /// <summary>Login without SMS. Works only in the Development environment (AUTH-06).</summary>
    public bool DevLogin { get; set; }

    /// <summary>AUTH-18: phone numbers of the admins, comma separated. Only in the file or environment, never in the panel.</summary>
    public string? AdminPhones { get; set; }

    public int MaxNameLength { get; set; }

    /// <summary>Zero bits the proof of work must have (SEC-06).</summary>
    public int PowDifficultyBits { get; set; }

    public int PowChallengeSeconds { get; set; }

    public int SmsCodeDigits { get; set; }

    public int SmsCodeSeconds { get; set; }

    /// <summary>Wrong tries allowed on one code (AUTH-12).</summary>
    public int SmsCodeTries { get; set; }

    /// <summary>Time between two SMS to one number (SEC-07).</summary>
    public int SmsWaitSeconds { get; set; }

    /// <summary>SEC-07: SMS to one number in 24 hours with the short wait; after that the wait is <see cref="SmsLongWaitSeconds"/>.</summary>
    public int SmsPerPhonePerDay { get; set; }

    public int SmsLongWaitSeconds { get; set; }

    /// <summary>High on purpose: many people can share one address (SEC-08).</summary>
    public int SmsPerIpPerDay { get; set; }

    /// <summary>The SMS budget of the whole server for 24 hours (SEC-08).</summary>
    public int SmsPerDay { get; set; }

    /// <summary>AUTH-24, AUTH-25, SEC-13: the email codes, like the SMS ones above.</summary>
    public int EmailCodeDigits { get; set; }

    public int EmailCodeSeconds { get; set; }

    public int EmailCodeTries { get; set; }

    public int EmailWaitSeconds { get; set; }

    public int EmailPerAddressPerDay { get; set; }

    public int EmailLongWaitSeconds { get; set; }

    public int EmailPerIpPerDay { get; set; }

    public int EmailPerDay { get; set; }

    public int PasswordMinLength { get; set; }

    public int PasswordMaxLength { get; set; }

    /// <summary>Argon2id memory in KiB (AUTH-13).</summary>
    public int PasswordMemoryKb { get; set; }

    /// <summary>Argon2id passes (AUTH-13).</summary>
    public int PasswordIterations { get; set; }

    /// <summary>Password hashes that may run at the same time. Each one takes PasswordMemoryKb. Read once at start.</summary>
    public int PasswordHashConcurrency { get; set; }

    /// <summary>Password tries for one login name in each window (SEC-09).</summary>
    public int PasswordTries { get; set; }

    /// <summary>Password tries from one client address in each window. High: many people can share one address (SEC-09).</summary>
    public int PasswordTriesPerIp { get; set; }

    public int PasswordTriesWindowMinutes { get; set; }

    /// <summary>AUTH-21: lifetime of an access key.</summary>
    public int AccessKeyDays { get; set; }

    /// <summary>AUTH-21: access keys one user may have.</summary>
    public int MaxAccessKeys { get; set; }

    /// <summary>AUTH-27 to AUTH-29.</summary>
    public GitHubOptions GitHub { get; set; } = new();
}

/// <summary>Section "Identity:GitHub": the GitHub OAuth app. Login with GitHub is on when the id and the secret are set.</summary>
public sealed class GitHubOptions
{
    public string? ClientId { get; set; }

    /// <summary>A secret: it lives only in the server's environment.</summary>
    public string? ClientSecret { get; set; }

    /// <summary>Must be the same as the "Authorization callback URL" of the GitHub app.</summary>
    public string? CallbackUrl { get; set; }

    public string? WebUrl { get; set; }

    public string? ApiUrl { get; set; }

    /// <summary>Time the person has at GitHub before the state cookie ends.</summary>
    public int StateSeconds { get; set; }

    /// <summary>Wait for each call to GitHub. Short: GitHub may be cut off from Iran.</summary>
    public int TimeoutSeconds { get; set; }

    /// <summary>Tries of each call when GitHub does not answer: the link from Iran to github.com stalls now and then.</summary>
    public int Tries { get; set; }

    public bool IsOn => !string.IsNullOrEmpty(ClientId) && !string.IsNullOrEmpty(ClientSecret);
}
