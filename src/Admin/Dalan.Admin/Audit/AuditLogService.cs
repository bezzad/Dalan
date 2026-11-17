using Dalan.Admin.Contracts;
using Dalan.Admin.Data;

namespace Dalan.Admin.Audit;

/// <summary>ADM-09.</summary>
public sealed class AuditLogService(Microsoft.EntityFrameworkCore.IDbContextFactory<AdminDb> dbFactory, TimeProvider time) : IAuditLog
{
    public async Task RecordAsync(long userId, string action, string target, string? detail, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        db.Audit.Add(new AuditEntry { UserId = userId, Action = action, Target = target, Detail = detail, At = time.GetUtcNow() });
        await db.SaveChangesAsync(cancellationToken);
    }
}
