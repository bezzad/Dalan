namespace Dalan.Relay.Chats;

public enum MembershipState
{
    Active = 0,
    Stopped = 1,
    Blocked = 2,
}

/// <summary>One user in one bot. This pair is the chat.</summary>
public sealed class Membership
{
    public long BotId { get; set; }
    public long UserId { get; set; }
    public MembershipState State { get; set; }
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>Sequence number of the newest message in this chat (REL-06).</summary>
    public long LastSeq { get; set; }
}
