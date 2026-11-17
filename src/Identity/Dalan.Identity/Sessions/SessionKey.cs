using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Dalan.Identity.Sessions;

/// <summary>AUTH-07: the key that signs sessions. From the settings, or a temporary one that dies with the process.</summary>
internal sealed partial class SessionKey : IDisposable
{
    private readonly ECDsa _ecdsa;

    public SessionKey(IOptions<IdentityOptions> options, ILogger<SessionKey> logger)
    {
        if (string.IsNullOrWhiteSpace(options.Value.SigningKey))
        {
            _ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            LogTemporaryKey(logger);
        }
        else
        {
            _ecdsa = ECDsa.Create();
            _ecdsa.ImportPkcs8PrivateKey(Convert.FromBase64String(options.Value.SigningKey), out _);
        }

        Key = new ECDsaSecurityKey(_ecdsa);
    }

    public SecurityKey Key { get; }

    public void Dispose() => _ecdsa.Dispose();

    [LoggerMessage(LogLevel.Warning,
        "Identity:SigningKey is not set. Using a temporary key: every user is logged out when the server restarts.")]
    private static partial void LogTemporaryKey(ILogger logger);
}
