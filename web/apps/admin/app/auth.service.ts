import { HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import {
  catchError,
  finalize,
  BehaviorSubject,
  map,
  Observable,
  of,
  shareReplay,
  switchMap,
  tap,
  throwError
} from 'rxjs';
import { environment } from '../environments/environment';

interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expires: string;
}

interface User {
  id: string;
  email: string;
  displayName: string;
  roles: string[];
  isFirstLogin: boolean;
  tenant?: { id: string; name: string; slug: string; plan: string };
}

const ACCESS_KEY = 'auth:access_token';
const REFRESH_KEY = 'auth:refresh_token';
const EXPIRES_KEY = 'auth:expires';
const REFRESH_SKEW_MS = 60_000;

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

  private refreshInFlight$: Observable<void> | null = null;

  constructor() {
    queueMicrotask(() => this.restoreSession());
  }

  register(body: { name: string; displayName: string; email: string; password: string }): Observable<void> {
    return this.http.post<AuthResponse>(`${this.baseUrl}/api/public/tenants/register`, body).pipe(
      switchMap(response => {
        this.storeTokens(response);
        return this.fetchUser().pipe(map(() => void 0));
      })
    );
  }

  login(email: string, password: string): Observable<void> {
    return this.http.post<AuthResponse>(`${this.baseUrl}/api/app/auth/login`, { email, password }).pipe(
      switchMap(response => {
        if (!response.accessToken || !response.refreshToken) {
          return throwError(() => new Error('Invalid login response'));
        }

        this.storeTokens(response);
        return this.fetchUser().pipe(map(() => void 0));
      }),
      catchError(error => throwError(() => error))
    );
  }

  logout(): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/api/app/auth/logout`, {}).pipe(
      catchError(() => of(void 0)),
      tap(() => this.clearSession())
    );
  }

  refreshToken(): Observable<void> {
    const refreshToken = this.getRefreshToken();
    const accessToken = this.getAccessToken();
    if (!refreshToken || !accessToken) {
      this.clearSession();
      return throwError(() => new Error('No refresh token'));
    }

    if (!this.refreshInFlight$) {
      this.refreshInFlight$ = this.http.post<AuthResponse>(`${this.baseUrl}/api/app/auth/refresh`, {
        accessToken,
        refreshToken
      }).pipe(
        tap(response => {
          if (!response.accessToken || !response.refreshToken) {
            throw new Error('Invalid refresh response');
          }
          this.storeTokens(response);
        }),
        map(() => void 0),
        catchError(error => {
          this.clearSession();
          return throwError(() => error);
        }),
        finalize(() => {
          this.refreshInFlight$ = null;
        }),
        shareReplay({ bufferSize: 1, refCount: false })
      );
    }

    return this.refreshInFlight$;
  }

  isAuthenticated(): boolean {
    return this.currentUserSubject.value !== null;
  }

  currentUser(): User | null {
    return this.currentUserSubject.value;
  }

  accessTokenExpiringSoon(): boolean {
    const raw = localStorage.getItem(EXPIRES_KEY);
    if (!raw) return !!this.getAccessToken();
    const expires = Date.parse(raw);
    if (Number.isNaN(expires)) return true;
    return expires - Date.now() <= REFRESH_SKEW_MS;
  }

  getAccessToken(): string | null {
    return localStorage.getItem(ACCESS_KEY);
  }

  getRefreshToken(): string | null {
    return localStorage.getItem(REFRESH_KEY);
  }

  private restoreSession(): void {
    if (!this.getAccessToken() || !this.getRefreshToken()) {
      this.authReadySubject.next(true);
      return;
    }

    this.fetchUser().subscribe({
      next: () => this.authReadySubject.next(true),
      error: () => this.authReadySubject.next(true)
    });
  }

  private storeTokens(response: Pick<AuthResponse, 'accessToken' | 'refreshToken' | 'expires'>): void {
    localStorage.setItem(ACCESS_KEY, response.accessToken);
    localStorage.setItem(REFRESH_KEY, response.refreshToken);
    if (response.expires) localStorage.setItem(EXPIRES_KEY, response.expires);
  }

  private clearSession(): void {
    localStorage.removeItem(ACCESS_KEY);
    localStorage.removeItem(REFRESH_KEY);
    localStorage.removeItem(EXPIRES_KEY);
    this.currentUserSubject.next(null);
  }

  private fetchUser(): Observable<User> {
    return this.http.get<User>(`${this.baseUrl}/api/app/me`).pipe(
      tap(user => this.currentUserSubject.next(user)),
      catchError(error => {
        if (error instanceof HttpErrorResponse && error.status === 401) {
          this.clearSession();
        }
        return throwError(() => error);
      })
    );
  }
}
