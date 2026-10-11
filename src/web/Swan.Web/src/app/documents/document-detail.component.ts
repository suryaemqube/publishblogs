import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DocumentDetail, ProposedArticle, SwanApiService } from '../core/swan-api.service';

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

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id') ?? '';
    this.api.document(id).subscribe({
      next: (d) => this.doc.set(d),
      error: (e: HttpErrorResponse) =>
        this.error.set(e.status === 404 ? 'Document not found.' : `Could not load document (HTTP ${e.status}).`),
    });
  }

  toggle(article: ProposedArticle): void {
    this.openArticle.update((cur) => (cur === article.id ? null : article.id));
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

