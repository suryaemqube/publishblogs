using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace Swan.Infrastructure.Security;

/// <summary>
/// Argon2id password hashing (PLAN-v2 §11.1). Format:
/// argon2id$v=19$m=65536,t=3,p=4$&lt;salt&gt;$&lt;hash&gt;  (salt and hash are base64)
/// </summary>
public static class PasswordHasher
{
    private const int MemoryKiB = 65536;
    private const int Iterations = 3;
    private const int Parallelism = 4;
    private const int HashLength = 32;
    private const int SaltLength = 16;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        var hash = Derive(password, salt, MemoryKiB, Iterations, Parallelism, HashLength);
        return $"argon2id$v=19$m={MemoryKiB},t={Iterations},p={Parallelism}$" +
               $"{Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string stored)
    {
        var parts = stored.Split('$');
        // ["argon2id", "v=19", "m=..,t=..,p=..", salt, hash]
        if (parts.Length != 5 || parts[0] != "argon2id") return false;

        var cost = parts[2].Split(',');
        var m = int.Parse(cost[0].Replace("m=", ""));
        var t = int.Parse(cost[1].Replace("t=", ""));
        var p = int.Parse(cost[2].Replace("p=", ""));
        var salt = Convert.FromBase64String(parts[3]);
        var expected = Convert.FromBase64String(parts[4]);

        var actual = Derive(password, salt, m, t, p, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    /// <summary>A valid hash of a random value, used to equalise timing when the user does not exist.</summary>
    public static readonly string DummyHash = Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

    private static byte[] Derive(string password, byte[] salt, int m, int t, int p, int length)
    {
        using var argon = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = m,
            Iterations = t,
            DegreeOfParallelism = p,
        };
        return argon.GetBytes(length);
    }
}
