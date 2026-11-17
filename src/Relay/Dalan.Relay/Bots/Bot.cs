namespace Dalan.Relay.Bots;

public sealed class Bot
{
    public long Id { get; private set; }
    public required string Username { get; set; }
    public required string Name { get; set; }
    public long OwnerUserId { get; set; }

    /// <summary>SHA-256 of the token secret. The token itself is never stored (REL-03).</summary>
    public required byte[] TokenHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>REL-26: the command menu, stored as jsonb.</summary>
    public List<CommandEntry> Commands { get; set; } = [];

    /// <summary>REL-33: the last get-updates call, saved at most once per Relay:BotOnlineSeconds.</summary>
    public DateTimeOffset? LastSeenAt { get; set; }

    /// <summary>REL-38: set while an admin has suspended the bot.</summary>
    public DateTimeOffset? SuspendedAt { get; set; }
}

public sealed class CommandEntry
{
    public string Command { get; set; } = "";
    public string Description { get; set; } = "";
}
