import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, RouterLink } from '@angular/router';
import {
  ArticleDetail,
  DraftSendResult,
  SiteSummary,
  SwanApiService,
} from '../core/swan-api.service';

interface SendState {
  busy: boolean;
  result?: DraftSendResult;
  refusal?: string;
  error?: string;
}

@Component({
  selector: 'swan-article-detail',
  imports: [RouterLink, DatePipe],
  templateUrl: './article-detail.component.html',
  styleUrl: './article-detail.component.scss',
})
export class ArticleDetailComponent implements OnInit {
  private readonly api = inject(SwanApiService);
  private readonly route = inject(ActivatedRoute);

  readonly detail = signal<ArticleDetail | null>(null);
  readonly sites = signal<SiteSummary[]>([]);
  readonly error = signal<string | null>(null);
  readonly send = signal<Record<string, SendState>>({});

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id') ?? '';
    this.api.article(id).subscribe({
      next: (d) => this.detail.set(d),
      error: (e: HttpErrorResponse) => this.error.set(e.status === 404 ? 'Article not found.' : describe(e)),
    });
    this.api.sites().subscribe({ next: (s) => this.sites.set(s) });
  }

  publicationFor(siteId: string) {
    return this.detail()?.publications.find((p) => p.siteId === siteId) ?? null;
  }

  stateOf(siteId: string): SendState {
    return this.send()[siteId] ?? { busy: false };
  }

  /** Creates the draft on first send, updates it afterwards, and refuses published posts. */
  sendDraft(siteId: string): void {
    const d = this.detail();
    if (!d) return;
    this.patch(siteId, { busy: true, result: undefined, refusal: undefined, error: undefined });
    this.api.sendDraft(d.article.id, siteId).subscribe({
      next: (r) => {
        this.patch(siteId, { busy: false, result: r });
        this.api.article(d.article.id).subscribe({ next: (fresh) => this.detail.set(fresh) });
      },
      error: (e: HttpErrorResponse) => {
        if (e.status === 409) {
          const body = e.error as { error?: string };
          this.patch(siteId, { busy: false, refusal: body.error ?? 'The site refused the change.' });
        } else {
          this.patch(siteId, { busy: false, error: describe(e) });
        }
      },
    });
  }

  private patch(siteId: string, change: Partial<SendState>): void {
    this.send.update((all) => ({ ...all, [siteId]: { ...this.stateOf(siteId), ...change } }));
  }
}

function describe(e: HttpErrorResponse): string {
  const body = e.error as { error?: string; detail?: string; title?: string } | null;
  if (body && typeof body === 'object') return body.error ?? body.detail ?? body.title ?? `HTTP ${e.status}`;
  return e.status === 0 ? 'Cannot reach the Swan API (is it running on port 5197?)' : `HTTP ${e.status}`;
}
