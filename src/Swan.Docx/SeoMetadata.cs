using System.Text.RegularExpressions;

namespace Swan.Docx;

/// <summary>
/// SEO fields written at the end of an article in the house template (PLAN-v2 §8.5). Values are taken as written;
/// length checks only produce warnings so a writer can still proceed.
/// </summary>
public sealed record SeoMetadata(
    string? SeoTitle,
    string? MetaDescription,
    string? CanonicalPath,
    string? Slug,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Extracts the trailing "Suggested WordPress SEO &amp; GEO Metadata" block from one article's paragraphs.
/// Recognised lines: "Title Tag (50-60 chars): ...", "URL Slug: ...", "Meta Description (150-160 chars): ...".
/// </summary>
public static class SeoParser
{
    private static readonly Regex Heading = new(@"^suggested\s+wordpress\s+seo\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TitleTag = new(@"^title\s+tag\s*\(([^)]*)\)\s*:\s*(.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Slug = new(@"^url\s+slug\s*:\s*(.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Meta = new(@"^meta\s+description\s*\(([^)]*)\)\s*:\s*(.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Range = new(@"(\d+)\s*-\s*(\d+)", RegexOptions.Compiled);

    /// <summary>Returns the body without SEO lines, and the metadata if any was found.</summary>
    public static SeoMetadata? Extract(IEnumerable<DocxParagraph> paragraphs, out List<DocxParagraph> body)
    {
        body = [];
        string? title = null, meta = null, path = null;
        (int Min, int Max)? titleRange = null, metaRange = null;
        var found = false;

        foreach (var p in paragraphs)
        {
            var text = p.Text;
            if (text.Length == 0) { body.Add(p); continue; }

            if (Heading.IsMatch(text)) { found = true; continue; }

            var m = TitleTag.Match(text);
            if (m.Success)
            {
                found = true;
                title = m.Groups[2].Value.Trim();
                titleRange = ParseRange(m.Groups[1].Value) ?? (50, 60);
                continue;
            }

            m = Meta.Match(text);
            if (m.Success)
            {
                found = true;
                meta = m.Groups[2].Value.Trim();
                metaRange = ParseRange(m.Groups[1].Value) ?? (150, 160);
                continue;
            }

            m = Slug.Match(text);
            if (m.Success)
            {
                found = true;
                path = m.Groups[1].Value.Trim();
                continue;
            }

            body.Add(p);
        }

        if (!found) return null;

        var warnings = new List<string>();
        if (title is null) warnings.Add("SEO title (Title Tag) is missing.");
        if (meta is null) warnings.Add("Meta description is missing.");
        if (path is null) warnings.Add("URL slug is missing.");

        if (title is not null && titleRange is { } tr && (title.Length < tr.Min || title.Length > tr.Max))
            warnings.Add($"SEO title is {title.Length} characters; the template asks for {tr.Min}-{tr.Max}.");
        if (meta is not null && metaRange is { } mr && (meta.Length < mr.Min || meta.Length > mr.Max))
            warnings.Add($"Meta description is {meta.Length} characters; the template asks for {mr.Min}-{mr.Max}.");

        var slug = path is null ? null : path.Trim('/').Split('/').LastOrDefault();
        return new SeoMetadata(title, meta, path, string.IsNullOrWhiteSpace(slug) ? null : slug, warnings);
    }

    private static (int Min, int Max)? ParseRange(string hint)
    {
        var m = Range.Match(hint);
        return m.Success ? (int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value)) : null;
    }
}
