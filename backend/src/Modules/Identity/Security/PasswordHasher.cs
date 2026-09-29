using System.Security.Cryptography;
using System.Text;

namespace Akiron.Modules.Identity.Security;

internal interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string storedHash);

    /// <summary>Spends the same time as a real check, for sign-ins with an unknown e-mail address.</summary>
    void SimulateVerify(string password);
}

/// <summary>
/// PBKDF2-SHA512, 600 000 iterations (OWASP 2023 guidance), per-password salt, constant-time
/// comparison. Stored as <c>v1:salt:hash</c> so the parameters can change without breaking old hashes.
/// </summary>
internal sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const string Version = "v1";
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 600_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA512;

    private static readonly string DummyHash = new Pbkdf2PasswordHasher().Hash("akiron-timing-equaliser");

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Iterations, Algorithm, HashSize);

        return $"{Version}:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string storedHash)
    {
        var parts = storedHash.Split(':');
        if (parts is not [Version, var saltText, var hashText])
        {
            return false;
        }

        var salt = Convert.FromBase64String(saltText);
        var expected = Convert.FromBase64String(hashText);
        var actual = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Iterations, Algorithm, expected.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public void SimulateVerify(string password) => Verify(password, DummyHash);
}
