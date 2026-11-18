namespace Dalan.Notification.Contracts;

/// <summary>NTF-10: the only way a service sends an email. The provider behind it is a setting.</summary>
public interface IEmailSender
{
    /// <summary>False when no provider is set: the login screen then hides email login (AUTH-16).</summary>
    bool IsOn { get; }

    /// <summary>Sends a login code to an address in lower case. False when the email could not be sent.</summary>
    Task<bool> SendLoginCodeAsync(string email, string code, CancellationToken cancellationToken);
}
