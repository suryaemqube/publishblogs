import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  ConnectionTestReport,
  Discovery,
  SiteDetail,
  SiteInput,
  SwanApiService,
} from '../core/swan-api.service';

/**
 * Add or edit a client site. Discovery lists what WordPress exposes over REST so the writer picks the post type and
 * taxonomies instead of typing REST names. The password is never shown again; leave it blank on edit to keep it.
 */
@Component({
  selector: 'swan-site-form',
  imports: [FormsModule, RouterLink],
  templateUrl: './site-form.component.html',
  styleUrl: './site-form.component.scss',
})
export class SiteFormComponent implements OnInit {
  private readonly api = inject(SwanApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly siteId = signal<string | null>(null);
  readonly loading = signal(false);
  readonly loadError = signal<string | null>(null);
  readonly saving = signal(false);
  readonly saveError = signal<string | null>(null);
  readonly saved = signal<{ message: string; report?: ConnectionTestReport } | null>(null);

  readonly discovery = signal<Discovery | null>(null);
  readonly discovering = signal(false);
  readonly discoverError = signal<string | null>(null);

  // Form model. Kept plain so ngModel can bind to it directly.
  name = '';
  restOrigin = '';
  username = '';
  password = '';
  postTypeKey = '';
  restBase = '';
  writeYoastMeta = false;
  isEnabled = true;
  userAgent = '';
  readonly selectedTaxonomies = signal<string[]>([]);
  customTaxonomies = '';

  get isEdit(): boolean {
    return this.siteId() !== null;
  }

  ngOnInit(): void {
    const flash = (history.state as { flash?: { message: string; report?: ConnectionTestReport } } | null)?.flash;
    if (flash) this.saved.set(flash);

    const id = this.route.snapshot.paramMap.get('id');
    if (!id) return;
    this.siteId.set(id);
    this.loading.set(true);
    this.api.siteDetail(id).subscribe({
      next: (s) => {
        this.loading.set(false);
        this.fill(s);
      },
      error: (e: HttpErrorResponse) => {
        this.loading.set(false);
        this.loadError.set(describe(e));
      },
    });
  }

  private fill(s: SiteDetail): void {
    this.name = s.name;
    this.restOrigin = s.restOrigin;
    this.username = s.username;
    this.postTypeKey = s.postTypeKey;
    this.restBase = s.restBase;
    this.writeYoastMeta = s.writeYoastMeta;
    this.isEnabled = s.isEnabled;
    this.userAgent = s.userAgent ?? '';
    this.selectedTaxonomies.set([...s.taxonomies]);
  }

  toggleTaxonomy(slug: string, on: boolean): void {
    const current = this.selectedTaxonomies();
    this.selectedTaxonomies.set(on ? [...new Set([...current, slug])] : current.filter((t) => t !== slug));
  }

  addCustomTaxonomies(): void {
    const names = this.customTaxonomies.split(',').map((t) => t.trim()).filter((t) => t.length > 0);
    this.selectedTaxonomies.set([...new Set([...this.selectedTaxonomies(), ...names])]);
    this.customTaxonomies = '';
  }

  /** Picks a post type from discovery and fills its REST base. */
  pickPostType(slug: string): void {
    const entry = this.discovery()?.types.find((t) => t.slug === slug);
    this.postTypeKey = slug;
    if (entry) this.restBase = entry.restBase;
  }

  discover(): void {
    this.discovering.set(true);
    this.discoverError.set(null);
    const request = this.isEdit
      ? this.api.discoverStoredSite(this.siteId()!)
      : this.api.discoverSite(this.restOrigin, this.username, this.password);
    request.subscribe({
      next: (d) => {
        this.discovering.set(false);
        this.discovery.set(d);
        if (!this.postTypeKey && d.types.length === 1) this.pickPostType(d.types[0].slug);
      },
      error: (e: HttpErrorResponse) => {
        this.discovering.set(false);
        this.discoverError.set(describe(e));
      },
    });
  }

  canDiscover(): boolean {
    if (this.isEdit) return true;
    return this.restOrigin.trim().length > 0 && this.username.trim().length > 0 && this.password.length > 0;
  }

  save(): void {
    if (this.saving()) return;
    this.saveError.set(null);
    this.saved.set(null);
    this.saving.set(true);

    const input: SiteInput = {
      name: this.name.trim(),
      restOrigin: this.restOrigin.trim(),
      username: this.username.trim(),
      applicationPassword: this.password.length > 0 ? this.password : null,
      postTypeKey: this.postTypeKey.trim(),
      restBase: this.restBase.trim(),
      taxonomies: this.selectedTaxonomies(),
      writeYoastMeta: this.writeYoastMeta,
      isEnabled: this.isEnabled,
      userAgent: this.userAgent.trim().length > 0 ? this.userAgent.trim() : null,
    };

    if (this.isEdit) {
      this.api.updateSite(this.siteId()!, input).subscribe({
        next: () => {
          this.saving.set(false);
          this.password = '';
          this.saved.set({ message: 'Saved. The connection must be tested again before drafts are allowed. Use Test connection on the Sites screen.' });
        },
        error: (e: HttpErrorResponse) => {
          this.saving.set(false);
          this.saveError.set(describe(e));
        },
      });
      return;
    }

    this.api.createSite(input).subscribe({
      next: (r) => {
        this.saving.set(false);
        this.password = '';
        const flash = {
          message: r.report.passed ? 'Site added. Connection test passed.' : 'Site added, but the connection test failed. Fix the checks below, then test again.',
          report: r.report,
        };
        // Move to the edit view so later changes update this site, not create a second one. The result travels with the navigation.
        void this.router.navigate(['/sites', r.site.id, 'edit'], { replaceUrl: true, state: { flash } });
      },
      error: (e: HttpErrorResponse) => {
        this.saving.set(false);
        this.saveError.set(describe(e));
      },
    });
  }

  canSave(): boolean {
    if (this.saving()) return false;
    if (!this.name.trim() || !this.restOrigin.trim() || !this.username.trim()) return false;
    if (!this.postTypeKey.trim() || !this.restBase.trim()) return false;
    return this.isEdit || this.password.length > 0;
  }
}

function describe(e: HttpErrorResponse): string {
  const body = e.error as { error?: string; detail?: string; title?: string } | string | null;
  if (typeof body === 'object' && body) return body.error ?? body.detail ?? body.title ?? `HTTP ${e.status}`;
  return e.status === 0 ? 'Cannot reach the Swan API. Check that it is running.' : `HTTP ${e.status} ${e.statusText || ''}`.trim();
}
