import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { ArticleRecord, SiteSummary, SwanApiService } from '../core/swan-api.service';

@Component({
  selector: 'swan-articles',
  imports: [RouterLink, DatePipe],
  templateUrl: './articles.component.html',
  styleUrl: './articles.component.scss',
})
export class ArticlesComponent implements OnInit {
  private readonly api = inject(SwanApiService);

  readonly articles = signal<ArticleRecord[]>([]);
  readonly sites = signal<SiteSummary[]>([]);
  readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.api.articles().subscribe({
      next: (a) => this.articles.set(a),
      error: (e: HttpErrorResponse) => this.error.set(describe(e)),
    });

    // Client names for the Client column. Failing here only costs the names, so it is not shown as a page error.
    this.api.sites().subscribe({ next: (s) => this.sites.set(s), error: () => undefined });
  }

  siteName(id: string | null): string {
    if (!id) return 'Not set';
    return this.sites().find((s) => s.id === id)?.name ?? id;
  }
}

function describe(e: HttpErrorResponse): string {
  const body = e.error as { error?: string; detail?: string; title?: string } | null;
  if (body && typeof body === 'object') return body.error ?? body.detail ?? body.title ?? `HTTP ${e.status}`;
  return e.status === 0 ? 'Cannot reach the Swan API. Check that it is running.' : `HTTP ${e.status}`;
}
