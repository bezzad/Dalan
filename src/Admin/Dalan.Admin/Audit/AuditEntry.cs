namespace Dalan.Admin.Audit;

/// <summary>ADM-09: one admin or builder action.</summary>
public sealed class AuditEntry
{
    public long Id { get; set; }

    /// <summary>The user who did it.</summary>
    public long UserId { get; set; }

    public required string Action { get; set; }
    public required string Target { get; set; }
    public string? Detail { get; set; }

    /// <summary>UTC.</summary>
    public DateTimeOffset At { get; set; }
}
