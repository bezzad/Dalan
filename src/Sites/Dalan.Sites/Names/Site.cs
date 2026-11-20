namespace Dalan.Sites.Names;

/// <summary>SITE-01: a site is a name owned by one builder. Its address is "name.(sites domain)".</summary>
public sealed class Site
{
    public long Id { get; set; }
    public long OwnerUserId { get; set; }
    public required string Name { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
