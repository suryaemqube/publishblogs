namespace Swan.Docx;

/// <summary>
/// An inline picture from a document. Token is what the article HTML uses (src="swan-image:N") until the image is
/// uploaded to a site, when the token is replaced with the site's URL.
/// </summary>
public sealed record DocxImageAsset(
    int Ordinal,
    string Token,
    int ParagraphIndex,
    string FileName,
    string? MimeType,
    byte[] Bytes,
    string AltText,
    string? Problem);

/// <summary>Client rules for images (decided by the client): JPG or WebP only, 500 KB at most, alt text required.</summary>
public static class ImageRules
{
    public const long MaxBytes = 500L * 1024;

    /// <summary>Returns "image/jpeg" or "image/webp" from the file's signature, or null for anything else.</summary>
    public static string? DetectMime(byte[] b)
    {
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "image/jpeg";
        if (b.Length >= 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P')
            return "image/webp";
        return null;
    }

    /// <summary>Names the format for the writer's message. Only used when the format is not allowed.</summary>
    public static string DescribeFormat(byte[] b)
    {
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 'P' && b[2] == 'N' && b[3] == 'G') return "a PNG";
        if (b.Length >= 6 && b[0] == 'G' && b[1] == 'I' && b[2] == 'F') return "a GIF";
        if (b.Length >= 2 && b[0] == 'B' && b[1] == 'M') return "a BMP";
        if (b.Length >= 4 && (b[0] == 'I' && b[1] == 'I' || b[0] == 'M' && b[1] == 'M')) return "a TIFF";
        return "an unsupported format";
    }

    /// <summary>The first problem with the image, or null when it can be used.</summary>
    public static string? Problem(int ordinal, int paragraph, byte[]? bytes, string alt)
    {
        var label = $"Image {ordinal} (paragraph {paragraph})";
        if (bytes is null) return $"{label} is missing from the document.";
        if (string.IsNullOrWhiteSpace(alt))
            return $"{label} has no alt text. In Word, right-click the picture and choose Edit Alt Text.";
        if (DetectMime(bytes) is null)
            return $"{label} is {DescribeFormat(bytes)}. Save it as JPG or WebP and reinsert it.";
        if (bytes.Length > MaxBytes)
            return $"{label} is {bytes.Length / 1024} KB. The limit is 500 KB; compress it and reinsert it.";
        return null;
    }
}
