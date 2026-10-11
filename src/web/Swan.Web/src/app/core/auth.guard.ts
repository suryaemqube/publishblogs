import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

/** Route guard. Only checks that a session exists; the API enforces permissions on every call. */
export const authGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (!auth.isSignedIn()) return router.createUrlTree(['/login']);
  if (!auth.user()) {
    await auth.loadCurrentUser();
    if (!auth.user()) return router.createUrlTree(['/login']);
  }
  return true;
};
