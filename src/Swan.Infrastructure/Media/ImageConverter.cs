using ImageMagick;
using Swan.Infrastructure.Content;

namespace Swan.Infrastructure.Media;

/// <summary>
/// Turns any picture into the published format (WebP) and enforces the 500 KB limit on the result.
/// Word re-encodes inserted WebP pictures as PNG, so requiring the writer to save JPG/WebP inside Word cannot work.
/// JPEG and WebP files already within the limit are kept byte-for-byte.
/// </summary>
public static class ImageConverter
{
    public const long MaxBytes = 500L * 1024;
    private const uint MaxWidth = 1600;
    private static readonly int[] Qualities = [85, 75, 65, 55, 45];

    public sealed record Normalized(byte[] Bytes, string MimeType, string Extension, bool Converted);

    public static Normalized Normalize(byte[] input)
    {
        if (input.Length == 0) throw new ImageRejectedException("The image file is empty.");

        if (IsJpeg(input) && input.Length <= MaxBytes)
            return new Normalized(input, "image/jpeg", ".jpg", Converted: false);
        if (IsWebP(input) && input.Length <= MaxBytes)
            return new Normalized(input, "image/webp", ".webp", Converted: false);

        try
        {
            using var image = new MagickImage(input);
            image.Strip(); // remove metadata (camera data, location, Word's copies)

            foreach (var q in Qualities)
            {
                var data = Encode(image, q);
                if (data.Length <= MaxBytes) return new Normalized(data, "image/webp", ".webp", Converted: true);
            }

            // Still too large at the lowest quality: shrink the dimensions, then try again.
            if (image.Width > MaxWidth)
            {
                image.Resize(new MagickGeometry(MaxWidth, 0));
                foreach (var q in Qualities)
                {
                    var data = Encode(image, q);
                    if (data.Length <= MaxBytes) return new Normalized(data, "image/webp", ".webp", Converted: true);
                }
            }

            var last = Encode(image, 45);
            throw new ImageRejectedException(
                $"The picture is still {last.Length / 1024} KB after compression. The limit is 500 KB, so use a smaller picture.");
        }
        catch (MagickException)
        {
            throw new ImageRejectedException("This file could not be read as a picture.");
        }
    }

    private static byte[] Encode(MagickImage source, int quality)
    {
        using var copy = (MagickImage)source.Clone();
        copy.Format = MagickFormat.WebP;
        copy.Quality = (uint)quality;
        return copy.ToByteArray();
    }

    public static bool IsJpeg(byte[] b) => b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF;

    public static bool IsWebP(byte[] b) =>
        b.Length >= 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P';
}
