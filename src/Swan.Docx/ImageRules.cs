namespace Swan.Docx;

/// <summary>
/// An inline picture from a document. Token is what the article HTML uses (src="swan-image:N") until the image is
/// uploaded to a site, when the token is replaced with the site's URL.
/// MimeType is the format found in the document; the stored picture is converted to WebP (see ImageConverter).
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

/// <summary>
/// Picture checks that can be made from the document alone: alt text, and that the bytes are a picture.
/// Format conversion and the 500 KB limit are applied when the picture is stored.
/// </summary>
public static class ImageRules
{
    /// <summary>Format from the file's signature, or null when it is not a picture type Swan can convert.</summary>
    public static string? DetectFormat(byte[] b)
    {
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "jpeg";
        if (b.Length >= 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P') return "webp";
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 'P' && b[2] == 'N' && b[3] == 'G') return "png";
        if (b.Length >= 6 && b[0] == 'G' && b[1] == 'I' && b[2] == 'F') return "gif";
        if (b.Length >= 2 && b[0] == 'B' && b[1] == 'M') return "bmp";
        if (b.Length >= 4 && ((b[0] == 'I' && b[1] == 'I') || (b[0] == 'M' && b[1] == 'M'))) return "tiff";
        return null;
    }

    /// <summary>The first problem found in the document itself, or null. Picture size and format are checked later.</summary>
    public static string? Problem(int ordinal, int paragraph, byte[]? bytes, string alt)
    {
        var label = $"Image {ordinal} (paragraph {paragraph})";
        if (bytes is null) return $"{label} is missing from the document.";
        if (string.IsNullOrWhiteSpace(alt))
            return $"{label} has no alt text. In Word, right-click the picture and choose Edit Alt Text.";
        if (DetectFormat(bytes) is null)
            return $"{label} is not a picture Swan can read. Insert it again as a JPG, PNG or WebP picture.";
        return null;
    }
}
