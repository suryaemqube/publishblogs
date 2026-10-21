import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { RoleSummary, SwanApiService, UserSummary } from '../core/swan-api.service';

/**
 * Team (PLAN-v2 §12.1, under Settings): who can sign in and what their role allows.
 * Read and create only — the seed migration owns the role-to-permission mapping.
 */
@Component({
  selector: 'swan-team',
  imports: [DatePipe],
  templateUrl: './team.component.html',
  styleUrl: './team.component.scss',
})
export class TeamComponent implements OnInit {
  private readonly api = inject(SwanApiService);

  readonly users = signal<UserSummary[]>([]);
  readonly roles = signal<RoleSummary[]>([]);
  readonly loadError = signal<string | null>(null);

  readonly displayName = signal('');
  readonly email = signal('');
  readonly password = signal('');
  readonly selectedRoles = signal<string[]>([]);
  readonly saving = signal(false);
  readonly formError = signal<string | null>(null);
  readonly created = signal<{ email: string; displayName: string } | null>(null);

  ngOnInit(): void {
    this.reload();
    this.api.roles().subscribe({
      next: (r) => this.roles.set(r),
      error: (e: HttpErrorResponse) => this.loadError.set(describe(e)),
    });
  }

  toggleRole(name: string, on: boolean): void {
    this.selectedRoles.set(on
      ? [...this.selectedRoles(), name]
      : this.selectedRoles().filter((r) => r !== name));
  }

  /** A readable password the admin passes on out of band. Base64 keeps it alphanumeric and free of look-alikes. */
  generatePassword(): void {
    const bytes = new Uint8Array(18);
    crypto.getRandomValues(bytes);
    this.password.set(btoa(String.fromCharCode(...bytes)).replace(/[+/=]/g, '').slice(0, 20));
  }

  selectedDescription(): string {
    const names = this.selectedRoles();
    return this.roles()
      .filter((r) => names.includes(r.name))
      .map((r) => `${r.name}: ${r.description}`)
      .join(' ');
  }

  canSave(): boolean {
    return this.displayName().trim().length > 0
      && this.email().trim().length > 0
      && this.password().length >= 12
      && this.selectedRoles().length > 0
      && !this.saving();
  }

  save(): void {
    if (!this.canSave()) return;
    const name = this.displayName().trim();
    const address = this.email().trim();

    this.saving.set(true);
    this.formError.set(null);
    this.created.set(null);

    this.api.createUser({ displayName: name, email: address, password: this.password(), roles: this.selectedRoles() })
      .subscribe({
        next: () => {
          this.created.set({ email: address, displayName: name });
          this.displayName.set('');
          this.email.set('');
          this.password.set('');
          this.selectedRoles.set([]);
          this.saving.set(false);
          this.reload();
        },
        error: (e: HttpErrorResponse) => {
          this.formError.set(describe(e));
          this.saving.set(false);
        },
      });
  }

  private reload(): void {
    this.api.users().subscribe({
      next: (u) => this.users.set(u),
      error: (e: HttpErrorResponse) => this.loadError.set(describe(e)),
    });
  }
}

function describe(e: HttpErrorResponse): string {
  const body = e.error as { error?: string; detail?: string; title?: string } | null;
  if (body && typeof body === 'object') return body.error ?? body.detail ?? body.title ?? `HTTP ${e.status}`;
  return e.status === 0 ? 'Cannot reach the Swan API. Check that it is running.' : `HTTP ${e.status}`;
}
