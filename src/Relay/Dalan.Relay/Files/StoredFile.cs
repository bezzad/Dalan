using Dalan.Relay.Contracts;

namespace Dalan.Relay.Files;

/// <summary>A row of relay.files: one file on disk. The file name on disk is <see cref="Id"/>.</summary>
public sealed class StoredFile
{
    public required string Id { get; set; }

    /// <summary>The bot of the chat the file was first sent in. Only this bot may send it again or download it.</summary>
    public long BotId { get; set; }

    /// <summary>The user of that chat.</summary>
    public long UserId { get; set; }

    /// <summary>True when the bot uploaded it; false for a user's upload (counts against REL-28's daily limit).</summary>
    public bool FromBot { get; set; }
    public FileKind Kind { get; set; }
    public required string Name { get; set; }
    public required string ContentType { get; set; }
    public long Size { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>REL-30: the sweep deletes the file after this time.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    public BotFile ToBot() => new(Id, Kind, Name, ContentType, Size);
}
