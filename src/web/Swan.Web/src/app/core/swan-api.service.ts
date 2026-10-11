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

export interface ProposedArticle {
  id: string;
  index: number;
  paragraphFrom: number;
  paragraphTo: number;
  title: string;
  wordCount: number;
  isUnverified: boolean;
  state: string;
  contentHtml: string;
}

export interface DocumentDetail {
  id: string;
  fileName: string;
  status: 'Conforming' | 'Rejected' | 'ImportedUnverified';
  uploadedUtc: string;
  fileSizeBytes: number;
  totalParagraphs: number;
  importedWithOverride: boolean;
  duplicate: boolean;
  report: ConversionReport;
  candidates: ProposedArticle[];
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

  uploadDocument(file: File, importAnyway: boolean): Observable<DocumentDetail> {
    const form = new FormData();
    form.append('file', file, file.name);
    form.append('importAnyway', importAnyway ? 'true' : 'false');
    return this.http.post<DocumentDetail>(`${SWAN_API_BASE}/documents`, form);
  }

  templateUrl(): string {
    return `${SWAN_API_BASE}/documents/template`;
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
