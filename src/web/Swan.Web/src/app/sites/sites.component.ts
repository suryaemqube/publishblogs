import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { PermissionKeys } from '../core/permissions';
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
  /** True while an enable/disable change is in flight. */
  toggling: boolean;
  actionError?: string;
}

@Component({
  selector: 'swan-sites',
  imports: [RouterLink],
  templateUrl: './sites.component.html',
  styleUrl: './sites.component.scss',
})
export class SitesComponent implements OnInit {
  private readonly api = inject(SwanApiService);
  readonly auth = inject(AuthService);

  readonly sites = signal<SiteSummary[]>([]);
  readonly loadError = signal<string | null>(null);
  readonly state = signal<Record<string, SiteState>>({});

  get canManage(): boolean {
    return this.auth.hasPermission(PermissionKeys.SiteManage);
  }

  get canPublishDraft(): boolean {
    return this.auth.hasPermission(PermissionKeys.PublishingDraft);
  }

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.api.sites().subscribe({
      next: (s) => this.sites.set(s),
      error: (e: HttpErrorResponse) => this.loadError.set(describe(e)),
    });
  }

  stateOf(id: string): SiteState {
    return this.state()[id] ?? { testing: false, drafting: false, toggling: false };
  }

  host(site: SiteSummary): string {
    try {
      return new URL(site.restOrigin).host;
    } catch {
      return site.restOrigin;
    }
  }

  /**
   * Connection status chip. The saved status decides whether drafts are allowed, so it is shown first;
   * a test run in this session overlays it with the fresh result.
   */
  healthOf(site: SiteSummary): { cls: string; label: string } {
    const s = this.stateOf(site.id);
    if (s.testing) return { cls: 'chip-info', label: 'Testing' };
    if (s.reportError) return { cls: 'chip-danger', label: 'Error' };
    if (s.report) return s.report.passed ? { cls: 'chip-ok', label: 'Healthy' } : { cls: 'chip-danger', label: 'Failed' };
    switch (site.connectionStatus) {
      case 'Ready': return { cls: 'chip-ok', label: 'Ready' };
      case 'NotReady': return { cls: 'chip-danger', label: 'Not ready' };
      default: return { cls: 'chip-neutral', label: 'Not tested' };
    }
  }

  lastTested(site: SiteSummary): string | null {
    if (!site.lastTestedUtc) return null;
    const d = new Date(site.lastTestedUtc);
    return Number.isNaN(d.getTime()) ? null : d.toLocaleString();
  }

  /** Drafts need a passing test and an enabled site; the API enforces the same rule. */
  canDraft(site: SiteSummary): boolean {
    return site.isEnabled && site.connectionStatus === 'Ready';
  }

  runConnectionTest(site: SiteSummary): void {
    this.patch(site.id, { testing: true, reportError: undefined, report: undefined });
    this.api.connectionTest(site.id).subscribe({
      next: (r) => {
        this.patch(site.id, { testing: false, report: r });
        this.reload(); // the test records a new status, which gates drafts
      },
      error: (e: HttpErrorResponse) => this.patch(site.id, { testing: false, reportError: describe(e) }),
    });
  }

  setEnabled(site: SiteSummary, enabled: boolean): void {
    this.patch(site.id, { toggling: true, actionError: undefined });
    this.api.setSiteEnabled(site.id, enabled).subscribe({
      next: (r) => {
        this.sites.update((all) => all.map((s) => (s.id === r.site.id ? r.site : s)));
        this.patch(site.id, { toggling: false });
      },
      error: (e: HttpErrorResponse) => this.patch(site.id, { toggling: false, actionError: describe(e) }),
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
  return e.status === 0 ? 'Cannot reach the Swan API. Check that it is running.' : `HTTP ${e.status} ${e.statusText || ''}`.trim();
}
