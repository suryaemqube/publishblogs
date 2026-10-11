using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace Swan.Docx;

/// <summary>Raised for input that is not a safe, readable Word document. The message is shown to the writer.</summary>
public sealed class DocxFormatException(string message) : Exception(message);

/// <summary>Limits applied before any XML is parsed (PLAN-v2 §11.3).</summary>
public sealed record DocxLimits
{
    public long MaxFileBytes { get; init; } = 25L * 1024 * 1024;
    public long MaxUncompressedBytes { get; init; } = 100L * 1024 * 1024;
    public int MaxEntries { get; init; } = 500;
    public double MaxCompressionRatio { get; init; } = 100;
}

public sealed record DocxRun(string Text, bool Bold, bool Italic, string? Href);

/// <param name="Index">1-based paragraph number in the body, as a writer would count them.</param>
/// <param name="Style">Word style id (for example "Title", "Heading1", "SwanNote"), or null when unstyled.</param>
public sealed record DocxParagraph(
    int Index,
    string? Style,
    IReadOnlyList<DocxRun> Runs,
    int? NumId,
    int ListLevel,
    bool OrderedList)
{
    public string Text => string.Concat(Runs.Select(r => r.Text)).Trim();

    /// <summary>True when every non-empty run is bold. Used only to point writers at unstyled headings.</summary>
    public bool AllBold => Runs.Where(r => r.Text.Trim().Length > 0).All(r => r.Bold) && Text.Length > 0;
}

public sealed class DocxPackage
{
    public required IReadOnlyList<DocxParagraph> Paragraphs { get; init; }
    public required IReadOnlyDictionary<string, string> StyleNames { get; init; }
    public int TableCount { get; init; }
}

/// <summary>
/// Reads a .docx into a flat, ordered list of paragraphs with their style, runs and list information.
/// Parsing is direct OOXML (System.IO.Compression + XDocument with DTDs prohibited) so every guard is explicit.
/// </summary>
public static class DocxReader
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/package/2006/relationships";

    public static DocxPackage Read(Stream input, DocxLimits? limits = null)
    {
        limits ??= new DocxLimits();

        using var buffer = new MemoryStream();
        input.CopyTo(buffer, 81920);
        if (buffer.Length > limits.MaxFileBytes)
            throw new DocxFormatException($"File is larger than {limits.MaxFileBytes / (1024 * 1024)} MB.");
        if (buffer.Length < 4)
            throw new DocxFormatException("File is empty or too small to be a Word document.");

        buffer.Position = 0;
        var magic = new byte[4];
        buffer.ReadExactly(magic);
        if (magic[0] != 0x50 || magic[1] != 0x4B || magic[2] != 0x03 || magic[3] != 0x04)
            throw new DocxFormatException("File is not a .docx package (the content is not a zip archive).");
        buffer.Position = 0;

        using var zip = new ZipArchive(buffer, ZipArchiveMode.Read, leaveOpen: true);
        GuardZip(zip, limits);

        var documentEntry = zip.GetEntry("word/document.xml")
            ?? throw new DocxFormatException("Not a Word document: word/document.xml is missing.");

        var styleNames = ReadStyles(zip);
        var rels = ReadRelationships(zip);
        var numbering = ReadNumbering(zip);

        var doc = Load(documentEntry);
        var body = doc.Root?.Element(W + "body")
            ?? throw new DocxFormatException("Not a Word document: no body element.");

        var paragraphs = new List<DocxParagraph>();
        var tables = 0;
        foreach (var child in body.Elements())
        {
            if (child.Name == W + "tbl") { tables++; continue; }
            if (child.Name != W + "p") continue;
            paragraphs.Add(ParseParagraph(child, paragraphs.Count + 1, rels, numbering));
        }

        return new DocxPackage
        {
            Paragraphs = paragraphs,
            StyleNames = styleNames,
            TableCount = tables,
        };
    }

    private static void GuardZip(ZipArchive zip, DocxLimits limits)
    {
        if (zip.Entries.Count > limits.MaxEntries)
            throw new DocxFormatException("Package contains too many parts; it is not a normal Word document.");

        long total = 0;
        foreach (var e in zip.Entries)
        {
            total += e.Length;
            if (total > limits.MaxUncompressedBytes)
                throw new DocxFormatException("Package expands beyond the size limit; rejected for safety.");
            var compressed = Math.Max(1, e.CompressedLength);
            if (e.Length / (double)compressed > limits.MaxCompressionRatio && e.Length > 1024 * 1024)
                throw new DocxFormatException("Package has an implausible compression ratio; rejected for safety.");
        }
    }

    private static XDocument Load(ZipArchiveEntry entry)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, // no DOCTYPE, so no entity expansion or XXE
            XmlResolver = null,
        };
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, settings);
        return XDocument.Load(reader);
    }

    private static Dictionary<string, string> ReadStyles(ZipArchive zip)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var entry = zip.GetEntry("word/styles.xml");
        if (entry is null) return map;
        foreach (var s in Load(entry).Root?.Elements(W + "style") ?? [])
        {
            var id = (string?)s.Attribute(W + "styleId");
            var name = (string?)s.Element(W + "name")?.Attribute(W + "val");
            if (id is not null && name is not null) map[id] = name;
        }
        return map;
    }

    private static Dictionary<string, (string Target, bool External)> ReadRelationships(ZipArchive zip)
    {
        var map = new Dictionary<string, (string, bool)>(StringComparer.Ordinal);
        var entry = zip.GetEntry("word/_rels/document.xml.rels");
        if (entry is null) return map;
        foreach (var r in Load(entry).Root?.Elements(Rel + "Relationship") ?? [])
        {
            var id = (string?)r.Attribute("Id");
            var target = (string?)r.Attribute("Target");
            var external = string.Equals((string?)r.Attribute("TargetMode"), "External", StringComparison.OrdinalIgnoreCase);
            if (id is not null && target is not null) map[id] = (target, external);
        }
        return map;
    }

    /// <summary>numId -> (ordered?) for list level 0 and the given level, keyed by "numId:ilvl".</summary>
    private static Dictionary<string, bool> ReadNumbering(ZipArchive zip)
    {
        var result = new Dictionary<string, bool>(StringComparer.Ordinal);
        var entry = zip.GetEntry("word/numbering.xml");
        if (entry is null) return result;

        var root = Load(entry).Root;
        if (root is null) return result;

        var numToAbstract = root.Elements(W + "num").ToDictionary(
            n => (string)n.Attribute(W + "numId")!,
            n => (string?)n.Element(W + "abstractNumId")?.Attribute(W + "val"));

        var abstracts = root.Elements(W + "abstractNum").ToDictionary(
            a => (string)a.Attribute(W + "abstractNumId")!,
            a => a);

        foreach (var (numId, abstractId) in numToAbstract)
        {
            if (abstractId is null || !abstracts.TryGetValue(abstractId, out var abs)) continue;
            foreach (var lvl in abs.Elements(W + "lvl"))
            {
                var ilvl = (string?)lvl.Attribute(W + "ilvl") ?? "0";
                var fmt = (string?)lvl.Element(W + "numFmt")?.Attribute(W + "val") ?? "bullet";
                result[$"{numId}:{ilvl}"] = fmt is not ("bullet" or "none");
            }
        }
        return result;
    }

    private static DocxParagraph ParseParagraph(XElement p, int index,
        IReadOnlyDictionary<string, (string Target, bool External)> rels,
        IReadOnlyDictionary<string, bool> numbering)
    {
        var pPr = p.Element(W + "pPr");
        var style = (string?)pPr?.Element(W + "pStyle")?.Attribute(W + "val");

        string? numId = null;
        var level = 0;
        var numPr = pPr?.Element(W + "numPr");
        if (numPr is not null)
        {
            numId = (string?)numPr.Element(W + "numId")?.Attribute(W + "val");
            level = int.TryParse((string?)numPr.Element(W + "ilvl")?.Attribute(W + "val"), out var l) ? l : 0;
        }
        var ordered = numId is not null && numbering.TryGetValue($"{numId}:{level}", out var o) && o;

        var runs = new List<DocxRun>();
        foreach (var child in p.Elements())
        {
            if (child.Name == W + "r")
            {
                runs.Add(ParseRun(child, href: null));
            }
            else if (child.Name == W + "hyperlink")
            {
                var rid = (string?)child.Attribute(R + "id");
                string? href = null;
                if (rid is not null && rels.TryGetValue(rid, out var rel) && rel.External)
                    href = rel.Target;
                foreach (var r in child.Elements(W + "r"))
                    runs.Add(ParseRun(r, href));
            }
        }

        return new DocxParagraph(index, style, runs, numId is null ? null : int.Parse(numId), level, ordered);
    }

    private static DocxRun ParseRun(XElement r, string? href)
    {
        var rPr = r.Element(W + "rPr");
        var bold = IsOn(rPr?.Element(W + "b"));
        var italic = IsOn(rPr?.Element(W + "i"));

        var text = new System.Text.StringBuilder();
        foreach (var node in r.Elements())
        {
            if (node.Name == W + "t") text.Append(node.Value);
            else if (node.Name == W + "tab") text.Append('\t');
            else if (node.Name == W + "br" || node.Name == W + "cr") text.Append(' ');
        }
        return new DocxRun(text.ToString(), bold, italic, href);
    }

    private static bool IsOn(XElement? toggle)
    {
        if (toggle is null) return false;
        var v = (string?)toggle.Attribute(W + "val");
        return v is null or "1" or "true" or "on";
    }
}
