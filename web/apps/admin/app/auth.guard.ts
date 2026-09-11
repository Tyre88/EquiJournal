import { inject } from '@angular/core';
import { Router, CanActivateFn } from '@angular/router';
import { filter, map, take } from 'rxjs';
import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  return authService.authReady$.pipe(
    filter(ready => ready),
    take(1),
    map(() => authService.isAuthenticated() || router.createUrlTree(['/login']))
  );
};
