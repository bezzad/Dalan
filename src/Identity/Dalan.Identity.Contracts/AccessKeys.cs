namespace Dalan.Identity.Contracts;

/// <summary>AUTH-22: an access key lets a terminal or an AI agent call the builder doors as its user.</summary>
public static class AccessKeys
{
    /// <summary>
    /// The name of the authentication scheme that Identity registers for keys. A door asks for it in its policy;
    /// every other call accepts only the session cookie.
    /// </summary>
    public const string Scheme = "AccessKey";

    /// <summary>The authorization policy of a door that takes the session cookie or an access key.</summary>
    public const string SessionOrKeyPolicy = "SessionOrKey";
}
