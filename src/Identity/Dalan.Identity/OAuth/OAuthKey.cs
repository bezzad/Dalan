using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Dalan.Identity.OAuth;

/// <summary>AUTH-42: one public key of the JWKS. Never the private part.</summary>
internal sealed record JwkJson(string Kty, string Crv, string X, string Y, string Kid, string Use, string Alg);

internal sealed record JwksJson(JwkJson[] Keys);

/// <summary>
/// AUTH-42: the ES256 key that signs ID tokens, from Identity:OAuthSigningKey (PKCS#8, PEM or base64), or a temporary one that
/// dies with the process. AUTH-43: the key of the per-site user ids is made from it, so they stay the same while it does.
/// </summary>
internal sealed partial class OAuthKey : IDisposable
{
    private readonly ECDsa _ecdsa;
    private readonly byte[] _subjectKey;

    public OAuthKey(IOptions<IdentityOptions> options, ILogger<OAuthKey> logger)
    {
        var text = options.Value.OAuthSigningKey?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            _ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            LogTemporaryKey(logger);
        }
        else
        {
            _ecdsa = ECDsa.Create();
            if (text.Contains("-----BEGIN", StringComparison.Ordinal)) _ecdsa.ImportFromPem(text);
            else _ecdsa.ImportPkcs8PrivateKey(Convert.FromBase64String(text), out _);
            if (_ecdsa.KeySize != 256)
                throw new InvalidOperationException("Identity:OAuthSigningKey must be a P-256 (ES256) private key.");
        }

        var parameters = _ecdsa.ExportParameters(includePrivateParameters: true);
        _subjectKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, parameters.D!, outputLength: 32, info: Encoding.UTF8.GetBytes("dalan:oauth-sub"));
        var x = Base64Url.EncodeToString(parameters.Q.X);
        var y = Base64Url.EncodeToString(parameters.Q.Y);
        // RFC 7638: the thumbprint of the public key is its id, so a new key gets a new id by itself.
        var kid = Base64Url.EncodeToString(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Create(CultureInfo.InvariantCulture, $$"""{"crv":"P-256","kty":"EC","x":"{{x}}","y":"{{y}}"}"""))));
        Key = new ECDsaSecurityKey(_ecdsa) { KeyId = kid };
        Jwks = new JwksJson([new JwkJson("EC", "P-256", x, y, kid, "sig", SecurityAlgorithms.EcdsaSha256)]);
    }

    public SecurityKey Key { get; }

    public JwksJson Jwks { get; }

    /// <summary>AUTH-43: the user's id for one site: stable, and two sites cannot match their users. Never the phone or the real id.</summary>
    public string Subject(string clientId, long userId) => Base64Url.EncodeToString(
        HMACSHA256.HashData(_subjectKey, Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"{clientId}:{userId}"))));

    public void Dispose() => _ecdsa.Dispose();

    [LoggerMessage(LogLevel.Warning,
        "Identity:OAuthSigningKey is not set. Using a temporary key: the sites' user ids and keys change when the server restarts.")]
    private static partial void LogTemporaryKey(ILogger logger);
}
