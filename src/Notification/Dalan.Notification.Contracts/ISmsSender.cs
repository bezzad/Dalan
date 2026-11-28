namespace Dalan.Notification.Contracts;

/// <summary>NTF-01: the only way a service sends an SMS. The provider behind it is a setting.</summary>
public interface ISmsSender
{
    /// <summary>Sends a login code to a number like +989121234567. False when the SMS could not be sent.</summary>
    Task<bool> SendLoginCodeAsync(string phone, string code, CancellationToken cancellationToken);

    /// <summary>NTF-13: sends the short text of a notice to a number like +989121234567. False when the SMS could not be sent.</summary>
    Task<bool> SendNoticeAsync(string phone, string text, CancellationToken cancellationToken);
}
