import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

/**
 * API address. Relative so the same build works in production (Angular served from the API's wwwroot)
 * and in development (the dev server proxies /api to the API — see proxy.conf.json).
 */
export const SWAN_API_BASE = '/api';

export interface SiteSummary {
  id: string;
  name: string;
  restOrigin: string;
  postTypeKey: string;
  restBase: string;
  username: string;
  taxonomies: string[];
  writeYoastMeta: boolean;
  isEnabled: boolean;
  /** "Untested" | "Ready" | "NotReady". Drafts are only allowed when "Ready" and enabled. */
  connectionStatus: 'Untested' | 'Ready' | 'NotReady';
  lastTestedUtc: string | null;
  lastTestSummary: string | null;
}

export interface SiteDetail extends SiteSummary {
  userAgent: string | null;
  hasPassword: boolean;
}

/** Form values for creating or editing a site. The password is only sent when typed. */
export interface SiteInput {
  name: string;
  restOrigin: string;
  username: string;
  applicationPassword: string | null;
  postTypeKey: string;
  restBase: string;
  taxonomies: string[];
  writeYoastMeta: boolean;
  isEnabled: boolean;
  /** Optional. Only set where the client's server blocks the default agent. */
  userAgent: string | null;
}

export interface DiscoveredEntry {
  slug: string;
  restBase: string;
  name: string;
}

export interface Discovery {
  types: DiscoveredEntry[];
  taxonomies: DiscoveredEntry[];
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
  isHidden: boolean;
  acceptedArticles: number;
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

  documents(showHidden = false): Observable<DocumentSummary[]> {
    return this.http.get<DocumentSummary[]>(`${SWAN_API_BASE}/documents`, { params: { showHidden: showHidden ? 'true' : 'false' } });
  }

  hideDocument(id: string): Observable<unknown> {
    return this.http.post(`${SWAN_API_BASE}/documents/${encodeURIComponent(id)}/hide`, {});
  }

  restoreDocument(id: string): Observable<unknown> {
    return this.http.post(`${SWAN_API_BASE}/documents/${encodeURIComponent(id)}/restore`, {});
  }

  deleteDocumentPermanently(id: string): Observable<void> {
    return this.http.delete<void>(`${SWAN_API_BASE}/documents/${encodeURIComponent(id)}/permanent`);
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

  siteDetail(id: string): Observable<SiteDetail> {
    return this.http.get<SiteDetail>(`${SWAN_API_BASE}/sites/${encodeURIComponent(id)}`);
  }

  createSite(input: SiteInput): Observable<{ site: SiteSummary; report: ConnectionTestReport }> {
    return this.http.post<{ site: SiteSummary; report: ConnectionTestReport }>(`${SWAN_API_BASE}/sites`, input);
  }

  updateSite(id: string, input: SiteInput): Observable<{ site: SiteSummary }> {
    return this.http.put<{ site: SiteSummary }>(`${SWAN_API_BASE}/sites/${encodeURIComponent(id)}`, input);
  }

  setSiteEnabled(id: string, enabled: boolean): Observable<{ site: SiteSummary }> {
    return this.http.post<{ site: SiteSummary }>(`${SWAN_API_BASE}/sites/${encodeURIComponent(id)}/enabled`, { enabled });
  }

  /** Lists the post types and taxonomies WordPress exposes. Uses the typed credentials; saves nothing. */
  discoverSite(restOrigin: string, username: string, applicationPassword: string): Observable<Discovery> {
    return this.http.post<Discovery>(`${SWAN_API_BASE}/sites/discover`, { restOrigin, username, applicationPassword });
  }

  /** Same discovery, using the password already stored for the site (edit form). */
  discoverStoredSite(id: string): Observable<Discovery> {
    return this.http.post<Discovery>(`${SWAN_API_BASE}/sites/${encodeURIComponent(id)}/discover`, {});
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
