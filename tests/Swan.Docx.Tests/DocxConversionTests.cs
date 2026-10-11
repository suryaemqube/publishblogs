using System.IO.Compression;
using System.Text;
using Swan.Docx;
using Xunit;
using Xunit.Abstractions;

namespace Swan.Docx.Tests;

public class DocxConversionTests(ITestOutputHelper output)
{
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static ConversionResult ConvertFile(string name)
    {
        using var stream = File.OpenRead(Fixture(name));
        return HouseStyleConverter.Convert(DocxReader.Read(stream));
    }

    [Fact]
    public void Template_conforms_and_its_instruction_block_is_excluded()
    {
        var result = ConvertFile("Swan-Article-Template.docx");

        output.WriteLine($"Conforming={result.Conforming} candidates={result.Candidates.Count} instructionParas={result.TemplateInstructionParagraphs} notes={result.NotesStripped} labels={result.LabelsStripped}");
        foreach (var c in result.Candidates)
            output.WriteLine($"  #{c.CandidateIndex} paras {c.ParagraphFrom}-{c.ParagraphTo} words={c.WordCount} title='{c.Title}'");
        foreach (var w in result.Warnings) output.WriteLine($"  warn: {w}");

        Assert.True(result.Conforming);
        Assert.Equal(4, result.Candidates.Count); // the four September articles (PLAN-v2 §6.1)
        Assert.DoesNotContain(result.Candidates, c => c.Title.Contains("PLACEHOLDER"));
        Assert.DoesNotContain(result.Candidates, c => c.ContentHtml.Contains("Writer's note"));
    }

    [Fact]
    public void September_style_free_document_is_rejected_with_reasons()
    {
        var result = ConvertFile("Emqube Blogs Oct 2026.docx");

        output.WriteLine($"Conforming={result.Conforming} total={result.TotalParagraphs} unstyled={result.UnstyledParagraphs.Count} headingLike={result.HeadingLikeUnstyled.Count}");
        foreach (var r in result.RejectReasons) output.WriteLine($"  reject: {r}");
        foreach (var l in result.LinkIssues) output.WriteLine($"  link p{l.Paragraph}: {l.Problem} {l.Href}");
        foreach (var s in result.StyleCounts) output.WriteLine($"  style {s.Key}: {s.Value}");

        Assert.False(result.Conforming);
        Assert.Contains(result.RejectReasons, r => r.Contains("Title"));
        Assert.Empty(result.Candidates);
    }

    [Fact]
    public void Non_zip_content_is_refused_before_parsing()
    {
        using var s = new MemoryStream(Encoding.UTF8.GetBytes("this is not a word document at all"));
        Assert.Throws<DocxFormatException>(() => DocxReader.Read(s));
    }

    [Fact]
    public void Zip_without_document_xml_is_refused()
    {
        using var s = BuildZip(("hello.txt", "hi"));
        var ex = Assert.Throws<DocxFormatException>(() => DocxReader.Read(s));
        Assert.Contains("word/document.xml", ex.Message);
    }

    [Fact]
    public void Document_with_DTD_is_refused_so_entities_cannot_expand()
    {
        const string xml = """
            <?xml version="1.0"?>
            <!DOCTYPE w:document [ <!ENTITY x "boom"> ]>
            <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body><w:p><w:r><w:t>&x;</w:t></w:r></w:p></w:body></w:document>
            """;
        using var s = BuildZip(("word/document.xml", xml));
        Assert.ThrowsAny<Exception>(() => DocxReader.Read(s));
    }

    [Fact]
    public void Style_driven_structure_maps_to_house_html()
    {
        var doc = BuildDocx(
            P("Title", "Relocation Guide"),
            P("Normal", "Plain ", B("bold"), " text & <tags>"),
            P("Heading1", "A section"),
            P("ListParagraph", "item one", numId: 1, ordered: false),
            P("ListParagraph", "item two", numId: 1, ordered: false),
            P("Heading2", "Sub"),
            P("SwanNote", "Writer only"),
            P("SwanCta", "Request a Proposal"));

        using var s = doc;
        var result = HouseStyleConverter.Convert(DocxReader.Read(s));

        Assert.True(result.Conforming);
        var c = Assert.Single(result.Candidates);
        Assert.Equal("Relocation Guide", c.Title);
        Assert.Contains("<p>Plain <strong>bold</strong> text &amp; &lt;tags&gt;</p>", c.ContentHtml);
        Assert.Contains("<h2>A section</h2>", c.ContentHtml);
        Assert.Contains("<ul><li>item one</li><li>item two</li></ul>", c.ContentHtml);
        Assert.Contains("<h3>Sub</h3>", c.ContentHtml);
        Assert.Contains("swan-cta", c.ContentHtml);
        Assert.DoesNotContain("Writer only", c.ContentHtml);
        Assert.Equal(1, result.NotesStripped);
    }

    [Fact]
    public void Bold_unstyled_paragraph_never_becomes_a_heading()
    {
        var doc = BuildDocx(
            P("Title", "Article"),
            P(null, "Zero Monthly Markup: the rent is the same", bold: true),
            P("Heading1", "Real heading"));

        var result = HouseStyleConverter.Convert(DocxReader.Read(doc));
        var html = Assert.Single(result.Candidates).ContentHtml;

        Assert.DoesNotContain("<h", html.Replace("<h2>Real heading</h2>", ""));
        Assert.Contains("<p><strong>Zero Monthly Markup: the rent is the same</strong></p>", html);
        Assert.Contains(2, result.HeadingLikeUnstyled);
    }

    [Fact]
    public void Each_title_starts_a_new_article_and_section_labels_end_the_previous_one()
    {
        var doc = BuildDocx(
            P("SwanLabel", "BLOGS"),
            P("Title", "First"),
            P("Normal", "one body"),
            P("SwanLabel", "BLOGS"),
            P("Title", "Second"),
            P("Normal", "two body"));

        var result = HouseStyleConverter.Convert(DocxReader.Read(doc));

        Assert.Equal(2, result.Candidates.Count);
        Assert.Equal("First", result.Candidates[0].Title);
        Assert.Equal("Second", result.Candidates[1].Title);
        Assert.DoesNotContain("BLOGS", result.Candidates[0].ContentHtml);
    }

    [Fact]
    public void Search_wrapped_links_are_flagged()
    {
        var doc = BuildDocx(
            P("Title", "Links"),
            P("Normal", "Read ", Link("Lady Packers", "https://www.google.com/search?q=https://emoversuae.com/x/%23lady_packers")));

        var result = HouseStyleConverter.Convert(DocxReader.Read(doc));

        Assert.Contains(result.LinkIssues, l => l.Problem.Contains("Search-results"));
    }

    // ---- helpers to build small in-memory documents ----------------------------------------

    private static MemoryStream BuildZip(params (string Name, string Content)[] entries)
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                using var w = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
                w.Write(content);
            }
        }
        ms.Position = 0;
        return ms;
    }

    private sealed record Piece(string Text, bool Bold = false, string? Link = null);

    private static Piece B(string text) => new(text, Bold: true);
    private static Piece Link(string text, string href) => new(text, Link: href);

    private static (string? Style, IEnumerable<Piece> Pieces, int? NumId, bool Ordered, bool Bold) P(
        string? style, string text, bool bold = false, int? numId = null, bool ordered = false) =>
        (style, [new Piece(text, bold)], numId, ordered, bold);

    private static (string? Style, IEnumerable<Piece> Pieces, int? NumId, bool Ordered, bool Bold) P(
        string? style, params object[] parts)
    {
        var pieces = parts.Select(x => x is Piece p ? p : new Piece((string)x)).ToList();
        return (style, pieces, null, false, false);
    }

    private static (string? Style, IEnumerable<Piece> Pieces, int? NumId, bool Ordered, bool Bold) P(
        string? style, string text, int numId, bool ordered) =>
        (style, [new Piece(text)], numId, ordered, false);

    private static MemoryStream BuildDocx(params (string? Style, IEnumerable<Piece> Pieces, int? NumId, bool Ordered, bool Bold)[] paras)
    {
        var body = new StringBuilder();
        var rels = new StringBuilder();
        var linkIndex = 0;
        var idx = 0;
        foreach (var (style, pieces, numId, ordered, _) in paras)
        {
            idx++;
            body.Append("<w:p>");
            var pPr = new StringBuilder();
            if (style is not null) pPr.Append($"<w:pStyle w:val=\"{style}\"/>");
            if (numId is not null) pPr.Append($"<w:numPr><w:ilvl w:val=\"0\"/><w:numId w:val=\"{numId}\"/></w:numPr>");
            if (pPr.Length > 0) body.Append($"<w:pPr>{pPr}</w:pPr>");

            foreach (var piece in pieces)
            {
                var run = $"<w:r>{(piece.Bold ? "<w:rPr><w:b/></w:rPr>" : "")}<w:t xml:space=\"preserve\">{System.Security.SecurityElement.Escape(piece.Text)}</w:t></w:r>";
                if (piece.Link is not null)
                {
                    linkIndex++;
                    var rid = $"rIdL{linkIndex}";
                    rels.Append($"<Relationship Id=\"{rid}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink\" Target=\"{System.Security.SecurityElement.Escape(piece.Link)}\" TargetMode=\"External\"/>");
                    body.Append($"<w:hyperlink r:id=\"{rid}\">{run}</w:hyperlink>");
                }
                else body.Append(run);
            }
            body.Append("</w:p>");
        }

        var document = $"""
            <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><w:body>{body}</w:body></w:document>
            """;
        var styles = """
            <w:styles xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
              <w:style w:styleId="Title"><w:name w:val="Title"/></w:style>
              <w:style w:styleId="Heading1"><w:name w:val="heading 1"/></w:style>
              <w:style w:styleId="Heading2"><w:name w:val="heading 2"/></w:style>
              <w:style w:styleId="ListParagraph"><w:name w:val="List Paragraph"/></w:style>
              <w:style w:styleId="SwanNote"><w:name w:val="Swan Note"/></w:style>
              <w:style w:styleId="SwanCta"><w:name w:val="Swan CTA"/></w:style>
              <w:style w:styleId="SwanLabel"><w:name w:val="Swan Label"/></w:style>
            </w:styles>
            """;
        var numbering = """
            <w:numbering xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
              <w:abstractNum w:abstractNumId="0"><w:lvl w:ilvl="0"><w:numFmt w:val="bullet"/></w:lvl></w:abstractNum>
              <w:num w:numId="1"><w:abstractNumId w:val="0"/></w:num>
            </w:numbering>
            """;
        var relsXml = $"""
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">{rels}</Relationships>
            """;

        return BuildZip(
            ("word/document.xml", document),
            ("word/styles.xml", styles),
            ("word/numbering.xml", numbering),
            ("word/_rels/document.xml.rels", relsXml));
    }
}
