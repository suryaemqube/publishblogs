import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { DocumentDetail, DocumentSummary, SWAN_API_BASE, SwanApiService } from '../core/swan-api.service';
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

  readonly selectedFile = signal<File | null>(null);
  readonly importAnyway = signal(false);
  readonly uploading = signal(false);
  readonly uploadError = signal<string | null>(null);
  readonly result = signal<DocumentDetail | null>(null);

  ngOnInit(): void {
    this.reload();
  }

  get canOverride(): boolean {
    return this.auth.hasPermission(PermissionKeys.ArticleImportUnverified);
  }

  reload(): void {
    this.api.documents().subscribe({
      next: (d) => this.documents.set(d),
      error: (e: HttpErrorResponse) => this.loadError.set(describe(e)),
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
    if (!file || this.uploading()) return;
    this.uploading.set(true);
    this.uploadError.set(null);
    this.result.set(null);
    this.api.uploadDocument(file, this.importAnyway()).subscribe({
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
  return e.status === 0 ? 'Cannot reach the Swan API (is it running on port 5197?)' : `HTTP ${e.status}`;
}

