import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../environments/environment';

@Injectable({ providedIn: 'root' })
export class Api {
  private http = inject(HttpClient);
  readonly base = environment.apiUrl;

  url(path: string): string {
    return `${this.base}${path.startsWith('/') ? path : '/' + path}`;
  }

  get<T>(path: string, params?: Record<string, string | number | boolean | undefined>) {
    let httpParams = new HttpParams();
    if (params) {
      for (const [key, value] of Object.entries(params)) {
        if (value !== undefined && value !== null && value !== '') {
          httpParams = httpParams.set(key, String(value));
        }
      }
    }
    return this.http.get<T>(this.url(path), { params: httpParams });
  }

  post<T>(path: string, body: unknown) {
    return this.http.post<T>(this.url(path), body);
  }

  put<T>(path: string, body: unknown) {
    return this.http.put<T>(this.url(path), body);
  }

  patch<T>(path: string, body: unknown) {
    return this.http.patch<T>(this.url(path), body);
  }

  delete<T>(path: string) {
    return this.http.delete<T>(this.url(path));
  }

  upload<T>(path: string, file: File) {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<T>(this.url(path), form);
  }

  download(path: string) {
    return this.http.post(this.url(path), {}, { responseType: 'blob' });
  }

  downloadGet(path: string, params?: Record<string, string | number | boolean | undefined>) {
    let httpParams = new HttpParams();
    if (params) {
      for (const [key, value] of Object.entries(params)) {
        if (value !== undefined && value !== null && value !== '') {
          httpParams = httpParams.set(key, String(value));
        }
      }
    }
    return this.http.get(this.url(path), { params: httpParams, responseType: 'blob' });
  }
}
