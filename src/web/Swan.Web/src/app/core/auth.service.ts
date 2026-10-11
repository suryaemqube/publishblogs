import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, firstValueFrom, tap } from 'rxjs';
import { SWAN_API_BASE } from './swan-api.service';

interface TokenResponse {
  accessToken: string;
  accessExpiresUtc: string;
  refreshToken: string;
  refreshExpiresUtc: string;
}

export interface CurrentUser {
  id: string;
  email: string;
  displayName: string;
  roles: string[];
  permissions: string[];
}

const SESSION_KEY = 'swan.session';

interface StoredSession {
  accessToken: string;
  accessExpiresUtc: string;
  refreshToken: string;
}

/**
 * Holds the session. The access token lives in memory only; the refresh token is kept in
 * sessionStorage (cleared when the browser tab closes). Dev trade-off: an httpOnly cookie is the
 * production choice, and needs the API to serve the web app or share a site.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private session: StoredSession | null = this.readSession();
  readonly user = signal<CurrentUser | null>(null);
  readonly isSignedIn = computed(() => this.session !== null);

  accessToken(): string | null {
    return this.session?.accessToken ?? null;
  }

  login(email: string, password: string): Observable<TokenResponse> {
    return this.http.post<TokenResponse>(`${SWAN_API_BASE}/auth/login`, { email, password }).pipe(
      tap((t) => this.store(t)),
    );
  }

  async loadCurrentUser(): Promise<void> {
    try {
      this.user.set(await firstValueFrom(this.http.get<CurrentUser>(`${SWAN_API_BASE}/auth/me`)));
    } catch {
      this.user.set(null);
    }
  }

  hasPermission(key: string): boolean {
    return this.user()?.permissions.includes(key) ?? false;
  }

  /** Exchanges the refresh token for a new pair. Returns false if the session is gone. */
  async refresh(): Promise<boolean> {
    const refreshToken = this.session?.refreshToken;
    if (!refreshToken) return false;
    try {
      const t = await firstValueFrom(
        this.http.post<TokenResponse>(`${SWAN_API_BASE}/auth/refresh`, { refreshToken }),
      );
      this.store(t);
      return true;
    } catch {
      this.clear();
      return false;
    }
  }

  async logout(): Promise<void> {
    const refreshToken = this.session?.refreshToken;
    this.clear();
    if (refreshToken) {
      try {
        await firstValueFrom(this.http.post(`${SWAN_API_BASE}/auth/logout`, { refreshToken }));
      } catch {
        /* the local session is already cleared */
      }
    }
    await this.router.navigateByUrl('/login');
  }

  clear(): void {
    this.session = null;
    this.user.set(null);
    sessionStorage.removeItem(SESSION_KEY);
  }

  private store(t: TokenResponse): void {
    this.session = {
      accessToken: t.accessToken,
      accessExpiresUtc: t.accessExpiresUtc,
      refreshToken: t.refreshToken,
    };
    sessionStorage.setItem(SESSION_KEY, JSON.stringify(this.session));
  }

  private readSession(): StoredSession | null {
    try {
      const raw = sessionStorage.getItem(SESSION_KEY);
      return raw ? (JSON.parse(raw) as StoredSession) : null;
    } catch {
      return null;
    }
  }
}
