using Dalan.Identity.SmsLogin;
using Dalan.Identity.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Dalan.Identity.Codes;

/// <summary>SEC-06: the challenge to solve before an SMS or a login email. It is tied to that one phone or email.</summary>
internal static class ChallengeEndpoint
{
    private sealed record ChallengeRequest(string? Phone, string? Email);

    public static void MapChallenge(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost("/api/auth/challenge", ChallengeAsync);

    private static async Task<IResult> ChallengeAsync(HttpContext http, ProofOfWork proofOfWork)
    {
        var request = await AuthJson.ReadAsync<ChallengeRequest>(http);
        var target = request?.Email is { } email ? EmailAddress.Normalize(email) : PhoneNumber.Normalize(request?.Phone);
        if (target is null) return request?.Email is null ? SmsLoginEndpoints.BadPhone() : EmailLogin.EmailLoginEndpoints.BadEmail();

        return AuthJson.Ok(new { Challenge = proofOfWork.NewChallenge(target), proofOfWork.Difficulty });
    }
}
