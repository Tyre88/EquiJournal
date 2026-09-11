import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { catchError, finalize, BehaviorSubject, map, Observable, of, switchMap, tap, throwError } from 'rxjs';
import { environment } from '../environments/environment';

interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expires: string;
  requiresTwoFactor?: boolean;
}

interface User {
  id: string;
  email: string;
  displayName: string;
  roles: string[];
  twoFactorEnabled: boolean;
  isFirstLogin: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private http = inject(HttpClient);
  private baseUrl = environment.apiUrl;

  private currentUserSubject = new BehaviorSubject<User | null>(null);
  public currentUser$ = this.currentUserSubject.asObservable();

  private authReadySubject = new BehaviorSubject(false);
  readonly authReady$ = this.authReadySubject.asObservable();

  private isRefreshing = false;
  private refreshTokenSubject = new BehaviorSubject<string | null>(null);

  constructor() {
    this.loadFromStorage();
  }

  login(email: string, password: string): Observable<{ requiresTwoFactor: boolean; canProceed?: boolean }> {
    return this.http.post<AuthResponse>(`${this.baseUrl}/api/app/auth/login`, { email, password }).pipe(
      switchMap(response => {
        if (!response.accessToken || !response.refreshToken) {
          return of({ requiresTwoFactor: !!response.requiresTwoFactor });
        }

        this.storeTokens(response.accessToken, response.refreshToken);

        if (response.requiresTwoFactor) {
          return of({ requiresTwoFactor: true });
        }

        return this.fetchUser().pipe(
          map(() => ({ requiresTwoFactor: false, canProceed: true }))
        );
      }),
      catchError(error => throwError(() => error))
    );
  }

  verify2fa(code: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.baseUrl}/api/app/auth/verify-2fa`, { code }).pipe(
      switchMap(response => {
        this.storeTokens(response.accessToken, response.refreshToken);
        return this.fetchUser().pipe(map(() => response));
      }),
      catchError(error => throwError(() => error))
    );
  }

  logout(): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/api/app/auth/logout`, {}).pipe(
      tap(() => {
        this.clearTokens();
        this.currentUserSubject.next(null);
      })
    );
  }

  refreshToken(): Observable<void> {
    const refreshToken = this.getRefreshToken();
    if (!refreshToken) {
      this.clearTokens();
      return of(void 0);
    }

    if (this.isRefreshing) {
      return new Observable(observer => {
        this.refreshTokenSubject.subscribe(token => {
          if (token) {
            observer.next();
            observer.complete();
          } else {
            observer.error('Refresh failed');
          }
        });
      });
    }

    this.isRefreshing = true;
    this.refreshTokenSubject.next(null);

    return this.http.post<AuthResponse>(`${this.baseUrl}/api/app/auth/refresh`, {
      accessToken: this.getAccessToken(),
      refreshToken
    }).pipe(
      tap(response => {
        this.storeTokens(response.accessToken, response.refreshToken);
        this.refreshTokenSubject.next(response.refreshToken);
      }),
      map(() => void 0),
      catchError(error => {
        this.clearTokens();
        this.refreshTokenSubject.next(null);
        return throwError(() => error);
      }),
      finalize(() => {
        this.isRefreshing = false;
      })
    );
  }

  isAuthenticated(): boolean {
    const user = this.currentUserSubject.value;
    return user !== null;
  }

  currentUser(): User | null {
    return this.currentUserSubject.value;
  }

  private loadFromStorage(): void {
    const accessToken = localStorage.getItem('auth:access_token');
    const refreshToken = localStorage.getItem('auth:refresh_token');

    if (accessToken && refreshToken) {
      this.fetchUser().subscribe({
        next: () => this.authReadySubject.next(true),
        error: () => this.authReadySubject.next(true)
      });
      return;
    }

    this.authReadySubject.next(true);
  }

  private storeTokens(accessToken: string, refreshToken: string): void {
    localStorage.setItem('auth:access_token', accessToken);
    localStorage.setItem('auth:refresh_token', refreshToken);
  }

  private clearTokens(): void {
    localStorage.removeItem('auth:access_token');
    localStorage.removeItem('auth:refresh_token');
  }

  private getAccessToken(): string | null {
    return localStorage.getItem('auth:access_token');
  }

  private getRefreshToken(): string | null {
    return localStorage.getItem('auth:refresh_token');
  }

  private fetchUser(): Observable<User> {
    return this.http.get<User>(`${this.baseUrl}/api/app/me`).pipe(
      tap(user => this.currentUserSubject.next(user)),
      catchError(error => {
        this.clearTokens();
        this.currentUserSubject.next(null);
        return throwError(() => error);
      })
    );
  }
}
