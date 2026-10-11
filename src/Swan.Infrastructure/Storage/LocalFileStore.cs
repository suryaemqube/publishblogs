namespace Swan.Infrastructure.Storage;

/// <summary>
/// Stores originals on the local disk. Dev implementation behind the file-store seam (PLAN-v2 §3, Storage).
/// Keys are content hashes, so the same file is never stored twice.
/// </summary>
public sealed class LocalFileStore(string root)
{
    public string Root { get; } = root;

    public static string DefaultRoot() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".swan-data", "documents");

    public async Task<string> SaveAsync(string key, byte[] content, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Root);
        var path = PathFor(key);
        if (!File.Exists(path))
        {
            // Write to a temp file first, so a crash never leaves a partial original under its final name.
            var temp = path + ".tmp";
            await File.WriteAllBytesAsync(temp, content, ct);
            File.Move(temp, path, overwrite: true);
        }
        return key;
    }

    public async Task<byte[]> ReadAsync(string key, CancellationToken ct = default) =>
        await File.ReadAllBytesAsync(PathFor(key), ct);

    private string PathFor(string key)
    {
        if (key.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_')))
            throw new ArgumentException("Invalid storage key.");
        return Path.Combine(Root, key);
    }
}
