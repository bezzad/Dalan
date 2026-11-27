using Dalan.Relay.Chats;
using Dalan.Relay.Contracts;
using Dalan.Relay.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Dalan.Relay.Files;

/// <summary>REL-29: a user may get only a file that a message of their chat carries, while it is kept.</summary>
internal sealed class UserFiles(IDbContextFactory<RelayDb> dbFactory, ChatService chats)
{
    public async Task<BotFile?> FindAsync(long userId, long botId, string fileId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var inChat = await db.Messages.AnyAsync(
            m => m.BotId == botId && m.UserId == userId && !m.Deleted && m.Content.File != null && m.Content.File.Id == fileId,
            cancellationToken);
        return inChat ? await chats.FindFileAsync(botId, fileId, cancellationToken) : null;
    }

    public static IResult NotFound() => ApiJson.Error(StatusCodes.Status404NotFound, "file_not_found",
        "This file is not in your chat, or it expired. A file is kept for a limited time.");
}
