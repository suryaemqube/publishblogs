import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

/** Dev API address. Must match Swan.Api launchSettings (http://localhost:5197). */
export const SWAN_API_BASE = 'http://localhost:5197/api';

export interface SiteSummary {
  id: string;
  name: string;
  restOrigin: string;
  postTypeKey: string;
  restBase: string;
}

export interface ConnectionCheck {
  name: string;
  passed: boolean;
  detail: string;
}

export interface ConnectionTestReport {
  passed: boolean;
  checks: ConnectionCheck[];
}

export interface DraftResult {
  remoteId: number;
  status: string;
  link: string | null;
  editUrl: string | null;
}

export interface CreateDraftBody {
  title: string;
  contentHtml: string;
  excerpt?: string | null;
  slug?: string | null;
}

export interface DocumentSummary {
  id: string;
  fileName: string;
  status: 'Conforming' | 'Rejected' | 'ImportedUnverified';
  uploadedUtc: string;
  fileSizeBytes: number;
  candidates: number;
  clientSiteId: string | null;
}

export interface LinkIssue {
  paragraph: number;
  href: string;
  problem: string;
}

export interface ConversionReport {
  conforming: boolean;
  rejectReasons: string[];
  warnings: string[];
  styleCounts: Record<string, number>;
  unstyledParagraphs: number[];
  headingLikeUnstyled: number[];
  linkIssues: LinkIssue[];
  notesStripped: number;
  labelsStripped: number;
  templateInstructionParagraphs: number;
  totalParagraphs: number;
}

export interface ArticleSeo {
  seoTitle: string | null;
  metaDescription: string | null;
  canonicalPath: string | null;
  slug: string | null;
  warnings: string[];
}

export interface ImageInfo {
  ordinal: number;
  token: string;
  paragraph: number;
  fileName: string;
  mimeType: string | null;
  altText: string;
  problem: string | null;
  ok: boolean;
}

export interface ProposedArticle {
  id: string;
  index: number;
  paragraphFrom: number;
  paragraphTo: number;
  title: string;
  wordCount: number;
  isUnverified: boolean;
  state: string;
  acceptedArticleId: string | null;
  images: ImageInfo[];
  contentHtml: string;
  seo: ArticleSeo;
}

export interface DocumentDetail {
  id: string;
  fileName: string;
  status: 'Conforming' | 'Rejected' | 'ImportedUnverified';
  uploadedUtc: string;
  fileSizeBytes: number;
  totalParagraphs: number;
  importedWithOverride: boolean;
  clientSiteId: string | null;
  duplicate: boolean;
  report: ConversionReport;
  candidates: ProposedArticle[];
}

export interface ArticleRecord {
  id: string;
  clientSiteId: string | null;
  featuredImage: { mediaId: string; altText: string } | null;
  title: string;
  status: string;
  isUnverified: boolean;
  sourceDocumentId: string;
  sourceCandidateId: string;
  slug: string | null;
  canonicalPath: string | null;
  seoTitle: string | null;
  metaDescription: string | null;
  contentHtml: string;
  createdUtc: string;
  updatedUtc: string;
}

export interface ArticlePublicationRecord {
  siteId: string;
  remotePostId: number | null;
  remoteStatus: string | null;
  editUrl: string | null;
  remoteLink: string | null;
  lastSyncedUtc: string;
}

export interface ArticleDetail {
  article: ArticleRecord;
  publications: ArticlePublicationRecord[];
  images: ImageInfo[];
  blockers: string[];
}

export interface DraftSendResult {
  action: 'created' | 'updated';
  remotePostId: number;
  remoteStatus: string;
  editUrl: string | null;
  remoteLink: string | null;
}

@Injectable({ providedIn: 'root' })
export class SwanApiService {
  private readonly http = inject(HttpClient);

  documents(): Observable<DocumentSummary[]> {
    return this.http.get<DocumentSummary[]>(`${SWAN_API_BASE}/documents`);
  }

  document(id: string): Observable<DocumentDetail> {
    return this.http.get<DocumentDetail>(`${SWAN_API_BASE}/documents/${encodeURIComponent(id)}`);
  }

  /** Sets the client on a document that has none. The server refuses to change a client once set. */
  setDocumentClient(documentId: string, siteId: string): Observable<{ clientSiteId: string; articlesUpdated: number }> {
    return this.http.put<{ clientSiteId: string; articlesUpdated: number }>(
      `${SWAN_API_BASE}/documents/${encodeURIComponent(documentId)}/client`, { siteId });
  }

  uploadDocument(file: File, siteId: string, importAnyway: boolean): Observable<DocumentDetail> {
    const form = new FormData();
    form.append('file', file, file.name);
    form.append('siteId', siteId);
    form.append('importAnyway', importAnyway ? 'true' : 'false');
    return this.http.post<DocumentDetail>(`${SWAN_API_BASE}/documents`, form);
  }

  templateUrl(): string {
    return `${SWAN_API_BASE}/documents/template`;
  }

  acceptCandidate(candidateId: string): Observable<{ article: ArticleDetail['article'] }> {
    return this.http.post<{ article: ArticleDetail['article'] }>(
      `${SWAN_API_BASE}/candidates/${encodeURIComponent(candidateId)}/accept`, {});
  }

  articles(): Observable<ArticleDetail['article'][]> {
    return this.http.get<ArticleDetail['article'][]>(`${SWAN_API_BASE}/articles`);
  }

  article(id: string): Observable<ArticleDetail> {
    return this.http.get<ArticleDetail>(`${SWAN_API_BASE}/articles/${encodeURIComponent(id)}`);
  }

  uploadFeaturedImage(articleId: string, file: File, altText: string): Observable<{ mediaId: string; fileName: string; altText: string }> {
    const form = new FormData();
    form.append('file', file, file.name);
    form.append('altText', altText);
    return this.http.post<{ mediaId: string; fileName: string; altText: string }>(
      `${SWAN_API_BASE}/articles/${encodeURIComponent(articleId)}/featured-image`, form);
  }

  featuredImage(articleId: string): Observable<Blob> {
    return this.http.get(`${SWAN_API_BASE}/articles/${encodeURIComponent(articleId)}/featured-image`, { responseType: 'blob' });
  }

  sendDraft(articleId: string, siteId: string): Observable<DraftSendResult> {
    return this.http.post<DraftSendResult>(
      `${SWAN_API_BASE}/articles/${encodeURIComponent(articleId)}/sites/${encodeURIComponent(siteId)}/draft`, {});
  }

  sites(): Observable<SiteSummary[]> {
    return this.http.get<SiteSummary[]>(`${SWAN_API_BASE}/sites`);
  }

  connectionTest(siteId: string): Observable<ConnectionTestReport> {
    return this.http.get<ConnectionTestReport>(`${SWAN_API_BASE}/sites/${encodeURIComponent(siteId)}/connection-test`);
  }

  createDraft(siteId: string, body: CreateDraftBody): Observable<DraftResult> {
    return this.http.post<DraftResult>(`${SWAN_API_BASE}/sites/${encodeURIComponent(siteId)}/drafts`, body);
  }
}
