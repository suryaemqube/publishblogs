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

@Injectable({ providedIn: 'root' })
export class SwanApiService {
  private readonly http = inject(HttpClient);

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
