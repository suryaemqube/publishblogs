import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

const ANONYMOUS_PATHS = ['/auth/login', '/auth/refresh', '/auth/logout'];

/** Adds the bearer token to API calls. On a 401, refreshes once and retries; otherwise sends the user to sign-in. */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (!req.url.includes('/api/') || ANONYMOUS_PATHS.some((p) => req.url.endsWith(p))) {
    return next(req);
  }

  const withToken = (r: HttpRequest<unknown>) => {
    const token = auth.accessToken();
    return token ? r.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : r;
  };

  return next(withToken(req)).pipe(
    catchError((err: unknown) => {
      if (!(err instanceof HttpErrorResponse) || err.status !== 401) return throwError(() => err);
      return from(auth.refresh()).pipe(
        switchMap((ok) => {
          if (!ok) {
            void router.navigate(['/login'], { queryParams: { reason: 'expired' } });
            return throwError(() => err);
          }
          return next(withToken(req));
        }),
      );
    }),
  );
};
