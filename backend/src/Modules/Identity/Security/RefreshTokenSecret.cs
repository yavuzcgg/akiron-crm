using System.Security.Cryptography;
using System.Text;

namespace Akiron.Modules.Identity.Security;

/// <summary>The raw refresh token goes to the browser once; only its hash is stored.</summary>
internal static class RefreshTokenSecret
{
    /// <summary>512 bits of entropy, hex-encoded so the cookie value needs no escaping.</summary>
    public static (string Raw, string Hash) Create()
    {
        var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(64));
        return (raw, Hash(raw));
    }

    public static string Hash(string raw) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
}
