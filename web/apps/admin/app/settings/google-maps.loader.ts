import { Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class GoogleMapsLoader {
  private pending?: Promise<boolean>;

  load(key: string): Promise<boolean> {
    if (!key) return Promise.resolve(false);
    const maps = (window as unknown as { google?: { maps?: { places?: unknown } } }).google?.maps;
    if (maps?.places) return Promise.resolve(true);
    if (this.pending) return this.pending;

    this.pending = new Promise<boolean>(resolve => {
      const existing = document.getElementById('google-maps-js');
      if (existing) {
        existing.addEventListener('load', () => resolve(!!(window as unknown as { google?: { maps?: { places?: unknown } } }).google?.maps?.places));
        existing.addEventListener('error', () => resolve(false));
        return;
      }
      const script = document.createElement('script');
      script.id = 'google-maps-js';
      script.async = true;
      script.src = `https://maps.googleapis.com/maps/api/js?key=${encodeURIComponent(key)}&libraries=places&language=sv&region=SE`;
      script.onload = () => resolve(true);
      script.onerror = () => resolve(false);
      document.head.appendChild(script);
    });
    return this.pending;
  }
}
