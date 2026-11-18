using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dalan.Identity.Users;
using Microsoft.Extensions.Options;

namespace Dalan.Identity.GitHubLogin;

/// <summary>A GitHub account as the login needs it. <see cref="Email"/> is the primary verified address in lower case, or null.</summary>
internal sealed record GitHubUser(long Id, string Name, string? Email);

/// <summary>
/// AUTH-28: the three calls of the GitHub OAuth web flow. The token is used for two reads and then forgotten.
/// Throws <see cref="HttpRequestException"/> when GitHub cannot be reached or refuses the code.
/// </summary>
internal sealed class GitHubClient(HttpClient http, IOptionsMonitor<IdentityOptions> options)
{
    private sealed record TokenJson(string? AccessToken, string? Error);

    private sealed record UserJson(long Id, string? Login, string? Name);

    private sealed record EmailJson(string? Email, bool Primary, bool Verified);

    public async Task<GitHubUser> GetUserAsync(string code, CancellationToken cancellationToken)
    {
        var github = options.CurrentValue.GitHub;
        var token = await GetTokenAsync(github, code, cancellationToken);

        var user = await GetAsync<UserJson>($"{github.ApiUrl}/user", token, cancellationToken);
        var emails = await GetAsync<EmailJson[]>($"{github.ApiUrl}/user/emails", token, cancellationToken);
        var email = emails.FirstOrDefault(e => e is { Primary: true, Verified: true })?.Email;

        var name = string.IsNullOrWhiteSpace(user.Name) ? user.Login ?? "" : user.Name;
        return new GitHubUser(user.Id, name.Trim(), EmailAddress.Normalize(email));
    }

    private async Task<string> GetTokenAsync(GitHubOptions github, string code, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{github.WebUrl}/login/oauth/access_token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = github.ClientId!,
                ["client_secret"] = github.ClientSecret!,
                ["code"] = code,
                ["redirect_uri"] = github.CallbackUrl ?? "",
            }),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        // A wrong or old code is still 200, with "error" instead of the token.
        var json = await response.Content.ReadFromJsonAsync<TokenJson>(AuthJson.Options, cancellationToken);
        return json?.AccessToken is { Length: > 0 } token
            ? token
            : throw new HttpRequestException($"GitHub refused the code: {json?.Error}.");
    }

    private async Task<T> GetAsync<T>(string url, string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(AuthJson.Options, cancellationToken)
            ?? throw new HttpRequestException($"GitHub sent an empty answer for {url}.");
    }
}
