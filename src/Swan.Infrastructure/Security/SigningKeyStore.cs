using System.Security.Cryptography;

namespace Swan.Infrastructure.Security;

/// <summary>
/// JWT signing key, kept outside the repository (default %USERPROFILE%\.swan-secrets\jwt-signing-key.txt).
/// Created on first use with 64 random bytes. Dev only: production uses Key Vault or DPAPI (PLAN-v2 §11.2).
/// </summary>
public static class SigningKeyStore
{
    public static byte[] LoadOrCreate(string path)
    {
        if (File.Exists(path) && new FileInfo(path).Length > 0)
            return Convert.FromBase64String(File.ReadAllText(path).Trim());

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var key = RandomNumberGenerator.GetBytes(64);
        File.WriteAllText(path, Convert.ToBase64String(key));
        return key;
    }
}
