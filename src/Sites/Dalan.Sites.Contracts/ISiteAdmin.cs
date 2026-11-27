namespace Dalan.Sites.Contracts;

/// <summary>What the admin door asks of Sites (ADM-19).</summary>
public interface ISiteAdmin
{
    /// <summary>Sites with a live agent session now.</summary>
    int CountOnline();
}
