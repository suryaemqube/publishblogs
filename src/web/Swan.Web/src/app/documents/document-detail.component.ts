import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DocumentDetail, ProposedArticle, SiteSummary, SwanApiService } from '../core/swan-api.service';

@Component({
  selector: 'swan-document-detail',
  imports: [RouterLink, DatePipe],
  templateUrl: './document-detail.component.html',
  styleUrl: './documents.component.scss',
})
export class DocumentDetailComponent implements OnInit {
  private readonly api = inject(SwanApiService);
  private readonly route = inject(ActivatedRoute);

  readonly doc = signal<DocumentDetail | null>(null);
  readonly error = signal<string | null>(null);
  readonly openArticle = signal<string | null>(null);
  readonly accepting = signal<string | null>(null);
  readonly acceptError = signal<string | null>(null);
  readonly accepted = signal<Record<string, string>>({});

  readonly sites = signal<SiteSummary[]>([]);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id') ?? '';
    this.api.document(id).subscribe({
      next: (d) => this.doc.set(d),
      error: (e: HttpErrorResponse) =>
        this.error.set(e.status === 404 ? 'Document not found.' : `Could not load document (HTTP ${e.status}).`),
    });
    this.api.sites().subscribe({ next: (s) => this.sites.set(s) });
  }

  siteName(id: string | null): string {
    if (!id) return 'Not set';
    return this.sites().find((s) => s.id === id)?.name ?? id;
  }

  readonly clientChoice = signal('');
  readonly settingClient = signal(false);
  readonly clientError = signal<string | null>(null);

  /** Only for documents uploaded before clients were chosen at upload. Once set, the client cannot change. */
  setClient(documentId: string): void {
    const siteId = this.clientChoice();
    if (!siteId || this.settingClient()) return;
    this.settingClient.set(true);
    this.clientError.set(null);
    this.api.setDocumentClient(documentId, siteId).subscribe({
      next: () => {
        this.settingClient.set(false);
        this.api.document(documentId).subscribe({ next: (d) => this.doc.set(d) });
      },
      error: (e: HttpErrorResponse) => {
        this.settingClient.set(false);
        this.clientError.set(e.error?.error ?? `Could not set the client (HTTP ${e.status}).`);
      },
    });
  }

  toggle(article: ProposedArticle): void {
    this.openArticle.update((cur) => (cur === article.id ? null : article.id));
  }

  /** Accepting turns the proposal into an article. Accepting twice returns the same article. */
  accept(article: ProposedArticle): void {
    this.accepting.set(article.id);
    this.acceptError.set(null);
    this.api.acceptCandidate(article.id).subscribe({
      next: (r) => {
        this.accepted.update((m) => ({ ...m, [article.id]: r.article.id }));
        this.accepting.set(null);
      },
      error: (e: HttpErrorResponse) => {
        this.acceptError.set(`Could not accept "${article.title}": ${e.error?.error ?? 'HTTP ' + e.status}`);
        this.accepting.set(null);
      },
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

  styleRows(counts: Record<string, number>): { name: string; count: number }[] {
    return Object.entries(counts).map(([name, count]) => ({ name, count }));
  }
}

