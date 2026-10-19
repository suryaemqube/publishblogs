import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import { RouterLink } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { DocumentDetail, DocumentSummary, SiteSummary, SWAN_API_BASE, SwanApiService } from '../core/swan-api.service';
import { PermissionKeys } from '../core/permissions';

@Component({
  selector: 'swan-documents',
  imports: [RouterLink, DatePipe, DecimalPipe],
  templateUrl: './documents.component.html',
  styleUrl: './documents.component.scss',
})
export class DocumentsComponent implements OnInit {
  private readonly api = inject(SwanApiService);
  private readonly http = inject(HttpClient);
  readonly auth = inject(AuthService);

  readonly documents = signal<DocumentSummary[]>([]);
  readonly loadError = signal<string | null>(null);

  readonly sites = signal<SiteSummary[]>([]);
  /** The client the document is for. Required before upload; the article inherits it. */
  readonly selectedSiteId = signal<string>('');
  readonly selectedFile = signal<File | null>(null);
  readonly importAnyway = signal(false);
  readonly uploading = signal(false);
  readonly uploadError = signal<string | null>(null);
  readonly result = signal<DocumentDetail | null>(null);

  ngOnInit(): void {
    this.reload();
    this.api.sites().subscribe({
      next: (s) => {
        this.sites.set(s);
        if (!this.selectedSiteId() && s.length === 1) this.selectedSiteId.set(s[0].id);
      },
      error: (e: HttpErrorResponse) => this.uploadError.set(`Could not load clients: ${describe(e)}`),
    });
  }

  siteName(id: string | null): string {
    if (!id) return 'Not set';
    return this.sites().find((s) => s.id === id)?.name ?? id;
  }

  get canOverride(): boolean {
    return this.auth.hasPermission(PermissionKeys.ArticleImportUnverified);
  }

  /** One request returns every document; hidden ones are split out here so the count is known up front. */
  reload(): void {
    this.api.documents(true).subscribe({
      next: (d) => this.documents.set(d),
      error: (e: HttpErrorResponse) => this.loadError.set(describe(e)),
    });
  }

  readonly showHidden = signal(false);
  readonly actionError = signal<string | null>(null);
  readonly visibleDocs = computed(() => this.documents().filter((d) => !d.isHidden));
  readonly hiddenDocs = computed(() => this.documents().filter((d) => d.isHidden));

  hide(d: DocumentSummary): void {
    this.runAction(this.api.hideDocument(d.id), `Could not hide ${d.fileName}`);
  }

  restore(d: DocumentSummary): void {
    this.runAction(this.api.restoreDocument(d.id), `Could not restore ${d.fileName}`);
  }

  /** Irreversible. Only offered for hidden documents with no accepted articles; the server checks again. */
  deletePermanently(d: DocumentSummary): void {
    if (!confirm(`Delete "${d.fileName}" permanently? Its original file and proposed articles are removed. This cannot be undone.`)) return;
    this.actionError.set(null);
    this.api.deleteDocumentPermanently(d.id).subscribe({
      next: () => this.reload(),
      error: (e: HttpErrorResponse) => this.actionError.set(`Could not delete ${d.fileName}: ${describe(e)}`),
    });
  }

  private runAction(obs: Observable<unknown>, failure: string): void {
    this.actionError.set(null);
    obs.subscribe({
      next: () => this.reload(),
      error: (e: HttpErrorResponse) => this.actionError.set(`${failure}: ${describe(e)}`),
    });
  }

  onFileChosen(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile.set(input.files?.[0] ?? null);
    this.result.set(null);
    this.uploadError.set(null);
  }

  upload(): void {
    const file = this.selectedFile();
    const siteId = this.selectedSiteId();
    if (!file || this.uploading()) return;
    if (!siteId) {
      this.uploadError.set('Choose the client this document is for before uploading.');
      return;
    }
    this.uploading.set(true);
    this.uploadError.set(null);
    this.result.set(null);
    this.api.uploadDocument(file, siteId, this.importAnyway()).subscribe({
      next: (d) => {
        this.result.set(d);
        this.uploading.set(false);
        this.reload();
      },
      error: (e: HttpErrorResponse) => {
        this.uploadError.set(describe(e));
        this.uploading.set(false);
      },
    });
  }

  /** Fetched with the bearer token (a plain link would not send it), then saved as a file. */
  downloadTemplate(): void {
    this.http.get(`${SWAN_API_BASE}/documents/template`, { responseType: 'blob' }).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = 'Swan-Article-Template.docx';
        a.click();
        URL.revokeObjectURL(url);
      },
      error: (e: HttpErrorResponse) => this.uploadError.set(`Template download failed: ${describe(e)}`),
    });
  }

  statusLabel(status: string): { cls: string; text: string } {
    switch (status) {
      case 'Conforming': return { cls: 'chip-ok', text: 'Conforming' };
      case 'Rejected': return { cls: 'chip-danger', text: 'Rejected' };
      case 'ImportedUnverified': return { cls: 'chip-warn', text: 'Imported unverified' };
      default: return { cls: 'chip-neutral', text: status };
    }
  }
}

function describe(e: HttpErrorResponse): string {
  const body = e.error as { error?: string; detail?: string; title?: string } | null;
  if (body && typeof body === 'object') return body.error ?? body.detail ?? body.title ?? `HTTP ${e.status}`;
  return e.status === 0 ? 'Cannot reach the Swan API. Check that it is running.' : `HTTP ${e.status}`;
}

