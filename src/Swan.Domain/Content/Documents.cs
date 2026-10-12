namespace Swan.Domain.Content;

public enum DocumentStatus
{
    /// <summary>Met the house style. Articles were proposed for review.</summary>
    Conforming,
    /// <summary>Did not meet the house style. Nothing was proposed; the original is kept for audit.</summary>
    Rejected,
    /// <summary>Imported through the audited override. One article, marked unverified (PLAN-v2 §6.3.1).</summary>
    ImportedUnverified,
}

/// <summary>An uploaded .docx. The original file is immutable and identified by its SHA-256.</summary>
public sealed class ArticleDocument
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = "";
    public long FileSizeBytes { get; set; }
    public string ContentHash { get; set; } = "";     // SHA-256, lower-case hex
    public string StorageKey { get; set; } = "";
    public DocumentStatus Status { get; set; }
    public int TotalParagraphs { get; set; }
    public string ReportJson { get; set; } = "{}";   // conversion report (PLAN-v2 §6.6)
    public Guid UploadedByUserId { get; set; }
    public DateTime UploadedUtc { get; set; }
    public bool ImportedWithOverride { get; set; }
}

/// <summary>An article proposed by the split. No Article exists until the writer accepts it (PLAN-v2 §6.1).</summary>
public sealed class ArticleCandidate
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public int CandidateIndex { get; set; }
    public int ParagraphFrom { get; set; }
    public int ParagraphTo { get; set; }
    public string ProposedTitle { get; set; } = "";
    public string ContentHtml { get; set; } = "";
    public int WordCount { get; set; }
    public bool IsUnverified { get; set; }
    public string State { get; set; } = "Proposed";

    // SEO block from the end of the article (PLAN-v2 §8.5). Null when the writer did not supply it.
    public string? SeoTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? CanonicalPath { get; set; }
    public string? Slug { get; set; }
    public string? SeoWarnings { get; set; }   // newline-separated
}
