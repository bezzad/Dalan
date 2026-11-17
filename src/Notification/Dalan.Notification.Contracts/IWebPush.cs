namespace Dalan.Notification.Contracts;

/// <summary>What a push shows: the title, the text, the address a tap opens, and a tag (a newer push with the same tag replaces the older).</summary>
public sealed record PushNote(string Title, string Body, string Url, string Tag);

/// <summary>A browser's push subscription: the push service's address and the browser's two public keys (base64url).</summary>
public sealed record PushDevice(string Endpoint, string P256dh, string Auth);

public enum SubscribeResult
{
    Saved,

    /// <summary>NTF-04: an unknown push service, or keys of the wrong size.</summary>
    Refused,

    /// <summary>NTF-05: the server has no push keys.</summary>
    PushOff,
}

/// <summary>Web Push (NTF-03 to NTF-09): the subscriptions of each user and the sending.</summary>
public interface IWebPush
{
    /// <summary>The VAPID public key the browser subscribes with, or null when push is off.</summary>
    string? PublicKey { get; }

    Task<SubscribeResult> SubscribeAsync(long userId, PushDevice device, CancellationToken cancellationToken);

    /// <summary>Removes the subscription, but only when it belongs to this user.</summary>
    Task UnsubscribeAsync(long userId, string endpoint, CancellationToken cancellationToken);

    /// <summary>NTF-07: puts a push for every device of this user in the queue and returns at once.</summary>
    void Send(long userId, PushNote note);
}
