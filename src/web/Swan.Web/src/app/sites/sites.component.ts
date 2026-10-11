import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import {
  ConnectionTestReport,
  DraftResult,
  SiteSummary,
  SwanApiService,
} from '../core/swan-api.service';

interface SiteState {
  testing: boolean;
  report?: ConnectionTestReport;
  reportError?: string;
  drafting: boolean;
  draft?: DraftResult;
  draftError?: string;
}

@Component({
  selector: 'swan-sites',
  templateUrl: './sites.component.html',
  styleUrl: './sites.component.scss',
})
export class SitesComponent implements OnInit {
  private readonly api = inject(SwanApiService);

  readonly sites = signal<SiteSummary[]>([]);
  readonly loadError = signal<string | null>(null);
  readonly state = signal<Record<string, SiteState>>({});

  ngOnInit(): void {
    this.api.sites().subscribe({
      next: (s) => this.sites.set(s),
      error: (e: HttpErrorResponse) => this.loadError.set(describe(e)),
    });
  }

  stateOf(id: string): SiteState {
    return this.state()[id] ?? { testing: false, drafting: false };
  }

  host(site: SiteSummary): string {
    try {
      return new URL(site.restOrigin).host;
    } catch {
      return site.restOrigin;
    }
  }

  /** Connection status chip for the table, derived from the last test run in this session. */
  healthOf(site: SiteSummary): { cls: string; label: string } {
    const s = this.stateOf(site.id);
    if (s.testing) return { cls: 'chip-info', label: 'Testing' };
    if (s.reportError) return { cls: 'chip-danger', label: 'Error' };
    if (!s.report) return { cls: 'chip-neutral', label: 'Not tested' };
    return s.report.passed
      ? { cls: 'chip-ok', label: 'Healthy' }
      : { cls: 'chip-danger', label: 'Failed' };
  }

  runConnectionTest(site: SiteSummary): void {
    this.patch(site.id, { testing: true, reportError: undefined, report: undefined });
    this.api.connectionTest(site.id).subscribe({
      next: (r) => this.patch(site.id, { testing: false, report: r }),
      error: (e: HttpErrorResponse) => this.patch(site.id, { testing: false, reportError: describe(e) }),
    });
  }

  /** Creates ONE draft. The API forces status=draft; this action cannot publish. */
  createTestDraft(site: SiteSummary): void {
    this.patch(site.id, { drafting: true, draft: undefined, draftError: undefined });
    const stamp = new Date().toISOString().replace('T', ' ').slice(0, 16) + ' UTC';
    this.api
      .createDraft(site.id, {
        title: `[Swan test - delete me] ${stamp}`,
        contentHtml: '<p>Test draft created from the Swan web app to verify draft writes. Safe to delete.</p>',
      })
      .subscribe({
        next: (d) => this.patch(site.id, { drafting: false, draft: d }),
        error: (e: HttpErrorResponse) => this.patch(site.id, { drafting: false, draftError: describe(e) }),
      });
  }

  private patch(id: string, change: Partial<SiteState>): void {
    this.state.update((all) => ({ ...all, [id]: { ...this.stateOf(id), ...change } }));
  }
}

function describe(e: HttpErrorResponse): string {
  const body = e.error as { error?: string; detail?: string; title?: string } | string | null;
  if (typeof body === 'object' && body) return body.error ?? body.detail ?? body.title ?? `HTTP ${e.status}`;
  return e.status === 0 ? 'Cannot reach the Swan API (is it running on port 5197?)' : `HTTP ${e.status} ${e.statusText || ''}`.trim();
}
