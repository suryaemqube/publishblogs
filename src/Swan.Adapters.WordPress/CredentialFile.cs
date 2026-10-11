namespace Swan.Adapters.WordPress;

/// <summary>
/// Reads a key=value credentials file kept outside the repository (default:
/// %USERPROFILE%\.swan-secrets\creds.txt). Lines starting with # are comments.
/// Values are never logged or returned to the UI by callers in this codebase.
/// </summary>
public static class CredentialFile
{
    public static IReadOnlyDictionary<string, string> Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Credentials file not found: {path}");
        if (new FileInfo(path).Length == 0)
            throw new InvalidDataException($"Credentials file is empty: {path}");

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var sep = line.IndexOf('=');
            if (sep <= 0) continue;
            map[line[..sep].Trim()] = line[(sep + 1)..].Trim();
        }
        return map;
    }

    public static string Require(IReadOnlyDictionary<string, string> settings, string key) =>
        settings.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v)
            ? v
            : throw new InvalidDataException($"Missing '{key}' in credentials file.");

    public static string DefaultPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".swan-secrets", "creds.txt");
}
