namespace Dalan.Identity.Contracts;

/// <summary>What other services may know about a user. It has no phone number on purpose (AUTH-02).</summary>
public sealed record UserProfile(long Id, string DisplayName, string? Username);
