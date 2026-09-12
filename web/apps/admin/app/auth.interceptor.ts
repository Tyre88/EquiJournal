import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);

  if (isLoginOrRefresh(req)) {
    return next(req);
  }

  const send = () => {
    const token = auth.getAccessToken();
    const authed = token
      ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
      : req;

    return next(authed).pipe(
      catchError((err: HttpErrorResponse) => {
        if (err.status === 401 && auth.getRefreshToken()) {
          return auth.refreshToken().pipe(
            switchMap(() => {
              const renewed = auth.getAccessToken();
              const retry = renewed
                ? req.clone({ setHeaders: { Authorization: `Bearer ${renewed}` } })
                : req;
              return next(retry);
            })
          );
        }
        return throwError(() => err);
      })
    );
  };

  if (auth.getRefreshToken() && auth.accessTokenExpiringSoon()) {
    return auth.refreshToken().pipe(
      switchMap(() => send()),
      catchError(() => send())
    );
  }

  return send();
};

function isLoginOrRefresh(req: HttpRequest<unknown>): boolean {
  return req.url.includes('/auth/login') || req.url.includes('/auth/refresh');
}
