using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Swan.Docx;

/// <summary>One article proposed from a document. Not yet an Article: the writer accepts it first.</summary>
public sealed record CandidateDraft(
    int CandidateIndex,
    int ParagraphFrom,
    int ParagraphTo,
    string Title,
    string ContentHtml,
    int WordCount);

public sealed record LinkIssue(int Paragraph, string Href, string Problem);

/// <summary>Conformance result and conversion report for one document (PLAN-v2 §6.2, §6.3, §6.6).</summary>
public sealed record ConversionResult
{
    public required bool Conforming { get; init; }
    public required IReadOnlyList<string> RejectReasons { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }
    public required IReadOnlyList<CandidateDraft> Candidates { get; init; }
    public required IReadOnlyDictionary<string, int> StyleCounts { get; init; }
    public required IReadOnlyList<int> UnstyledParagraphs { get; init; }
    public required IReadOnlyList<int> HeadingLikeUnstyled { get; init; }
    public required IReadOnlyList<LinkIssue> LinkIssues { get; init; }
    public required int NotesStripped { get; init; }
    public required int LabelsStripped { get; init; }
    public required int TemplateInstructionParagraphs { get; init; }
    public required int TotalParagraphs { get; init; }
}

/// <summary>
/// Maps the house style to HTML. Structure comes only from named styles; nothing is inferred from
/// bold or position (PLAN-v2 §6.3). Styles are listed in <see cref="KnownStyles"/>.
/// </summary>
public static class HouseStyleConverter
{
    public static readonly IReadOnlySet<string> KnownStyles = new HashSet<string>(StringComparer.Ordinal)
    {
        "Normal", "Title", "Heading1", "Heading2", "Heading3", "ListParagraph", "Caption",
        "SwanLabel", "SwanNote", "SwanCta",
    };

    private static readonly Regex SearchWrapper = new(@"google\.[a-z.]+/search\?q=", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex PercentEncodedFragment = new(@"%23", RegexOptions.Compiled);

    public static ConversionResult Convert(DocxPackage package)
    {
        var reasons = new List<string>();
        var warnings = new List<string>();
        var candidates = new List<CandidateDraft>();
        var linkIssues = new List<LinkIssue>();
        var unstyled = new List<int>();
        var headingLike = new List<int>();
        var styleCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        int notes = 0, labels = 0, instructionParas = 0;

        var paras = package.Paragraphs;

        // Pass 1: conformance facts and link checks (every paragraph, including ones later excluded).
        foreach (var p in paras)
        {
            var key = p.Style ?? "(unstyled)";
            styleCounts[key] = styleCounts.GetValueOrDefault(key) + 1;

            if (p.Style is null)
            {
                if (p.Text.Length > 0) unstyled.Add(p.Index);
                if (p.AllBold && p.Text.Length is > 0 and <= 120 && !p.Text.EndsWith('.'))
                    headingLike.Add(p.Index);
            }
            else if (!KnownStyles.Contains(p.Style))
            {
                warnings.Add($"Paragraph {p.Index}: style '{p.Style}' is not part of the house style and was treated as Normal.");
            }

            foreach (var run in p.Runs)
            {
                if (run.Href is null) continue;
                if (SearchWrapper.IsMatch(run.Href))
                    linkIssues.Add(new LinkIssue(p.Index, run.Href, "Search-results URL wrapping a page link. Paste the real URL."));
                else if (PercentEncodedFragment.IsMatch(run.Href))
                    linkIssues.Add(new LinkIssue(p.Index, run.Href, "Fragment is percent-encoded (%23). Use # instead."));
            }
        }

        var titleCount = paras.Count(p => p.Style == "Title" && p.Text.Length > 0);
        if (titleCount == 0)
            reasons.Add("No paragraph uses the Title style. Every article must start with a Title paragraph.");

        // Pass 2: split and convert. Template instruction blocks are excluded, as the template asks writers to delete them.
        CandidateBuilder? current = null;
        var pendingBeforeFirst = 0;
        var inInstructions = false;

        void Close()
        {
            if (current is null) return;
            var built = current.Build(candidates.Count + 1);
            if (built is null)
                warnings.Add($"Article starting at paragraph {current.From} has a title but no body, so it was not proposed.");
            else
                candidates.Add(built);
            current = null;
        }

        foreach (var p in paras)
        {
            if (p.Style == "SwanLabel")
            {
                labels++;
                var t = p.Text.ToUpperInvariant();
                if (t.StartsWith("INSTRUCTIONS", StringComparison.Ordinal)) { inInstructions = true; Close(); }
                else if (t.StartsWith("END INSTRUCTIONS", StringComparison.Ordinal)) { inInstructions = false; }
                else if (!inInstructions) Close(); // a section label ends the current article
                continue;
            }

            if (inInstructions)
            {
                instructionParas++;
                continue;
            }

            if (p.Style == "Title")
            {
                Close();
                if (p.Text.Length == 0)
                {
                    warnings.Add($"Paragraph {p.Index}: empty Title ignored.");
                    continue;
                }
                current = new CandidateBuilder(p.Index, p.Text);
                continue;
            }

            if (p.Style == "SwanNote")
            {
                notes++;
                continue;
            }

            if (current is null)
            {
                if (p.Text.Length > 0) pendingBeforeFirst++;
                continue;
            }

            current.Add(p);
        }
        Close();

        if (pendingBeforeFirst > 0)
            warnings.Add($"{pendingBeforeFirst} paragraph(s) before the first Title were not included in any article.");
        if (package.TableCount > 0)
            warnings.Add($"{package.TableCount} table(s) found. Tables are not converted yet and were left out.");

        var conforming = reasons.Count == 0;
        return new ConversionResult
        {
            Conforming = conforming,
            RejectReasons = reasons,
            Warnings = warnings,
            Candidates = conforming ? candidates : [],
            StyleCounts = styleCounts,
            UnstyledParagraphs = unstyled,
            HeadingLikeUnstyled = headingLike,
            LinkIssues = linkIssues,
            NotesStripped = notes,
            LabelsStripped = labels,
            TemplateInstructionParagraphs = instructionParas,
            TotalParagraphs = paras.Count,
        };
    }

    /// <summary>
    /// "Import anyway (unverified)" (PLAN-v2 §6.3.1). The whole document becomes one article with no split and no
    /// inference: styles are mapped as usual, everything else is a paragraph. The caller marks it unverified.
    /// </summary>
    public static CandidateDraft? ConvertAsSingleUnverifiedArticle(DocxPackage package, string title)
    {
        var builder = new CandidateBuilder(1, string.IsNullOrWhiteSpace(title) ? "Untitled" : title);
        foreach (var p in package.Paragraphs)
        {
            if (p.Style is "SwanLabel" or "SwanNote" or "Title") continue;
            builder.Add(p);
        }
        return builder.Build(1);
    }

    private sealed class CandidateBuilder
    {
        private readonly List<DocxParagraph> _body = [];
        private readonly int _from;
        private readonly string _title;
        private int _to;

        public CandidateBuilder(int from, string title)
        {
            _from = from;
            _title = title;
            _to = from;
        }

        public int From => _from;

        public void Add(DocxParagraph p)
        {
            _body.Add(p);
            _to = p.Index;
        }

        public CandidateDraft? Build(int index)
        {
            var html = new StringBuilder();
            var words = 0;
            var listOpen = (string?)null; // "ul" or "ol"
            int? listNum = null;

            void CloseList()
            {
                if (listOpen is null) return;
                html.Append("</").Append(listOpen).Append('>');
                listOpen = null;
                listNum = null;
            }

            foreach (var p in _body)
            {
                if (p.Text.Length == 0 && p.Style != "SwanCta") continue;

                switch (p.Style)
                {
                    case "ListParagraph":
                        var tag = p.OrderedList ? "ol" : "ul";
                        if (listOpen != tag || listNum != p.NumId)
                        {
                            CloseList();
                            html.Append('<').Append(tag).Append('>');
                            listOpen = tag;
                            listNum = p.NumId;
                        }
                        html.Append("<li>").Append(Inline(p)).Append("</li>");
                        break;

                    case "Heading1": CloseList(); html.Append("<h2>").Append(Inline(p)).Append("</h2>"); break;
                    case "Heading2": CloseList(); html.Append("<h3>").Append(Inline(p)).Append("</h3>"); break;
                    case "Heading3": CloseList(); html.Append("<h4>").Append(Inline(p)).Append("</h4>"); break;
                    case "Caption":  CloseList(); html.Append("<p class=\"caption\">").Append(Inline(p)).Append("</p>"); break;
                    case "SwanCta":  CloseList(); html.Append("<aside class=\"swan-cta\"><p>").Append(Inline(p)).Append("</p></aside>"); break;

                    case "SwanNote":
                    case "SwanLabel":
                        // Never published. Counted in the report by the caller.
                        continue;

                    default: // Normal, unstyled, and unknown styles (reported as warnings upstream)
                        CloseList();
                        html.Append("<p>").Append(Inline(p)).Append("</p>");
                        break;
                }
                words += CountWords(p.Text);
            }
            CloseList();

            if (html.Length == 0) return null;
            return new CandidateDraft(index, _from, _to, _title.Trim(), html.ToString(), words + CountWords(_title));
        }

        private static string Inline(DocxParagraph p)
        {
            var sb = new StringBuilder();
            foreach (var run in p.Runs)
            {
                if (run.Text.Length == 0) continue;
                var text = WebUtility.HtmlEncode(run.Text);
                if (run.Bold) text = $"<strong>{text}</strong>";
                if (run.Italic) text = $"<em>{text}</em>";
                if (run.Href is not null && IsSafeHref(run.Href))
                    text = $"<a href=\"{WebUtility.HtmlEncode(run.Href)}\">{text}</a>";
                sb.Append(text);
            }
            return Regex.Replace(sb.ToString(), @"[ \t]{2,}", " ").Trim();
        }

        private static bool IsSafeHref(string href) =>
            href.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || href.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
            || href.StartsWith("tel:", StringComparison.OrdinalIgnoreCase);

        private static int CountWords(string s) =>
            s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
