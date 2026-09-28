import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { EjPageHeaderComponent, ToastService } from '@equijournal/ui';
import { Api } from '../api';
import { AuthService } from '../auth.service';
import { ThemePreference, ThemeService } from '../theme.service';

@Component({
  selector: 'app-account-settings',
  standalone: true,
  imports: [FormsModule, EjPageHeaderComponent],
  template: `
    <ej-page-header title="Konto" subtitle="Namn, e-post och lösenord." />
    <div class="card">
      <h2 class="section-title">Utseende</h2>
      <p class="muted">Välj ljust, mörkt eller samma som enheten.</p>
      <div class="seg" role="radiogroup" aria-label="Tema">
        @for (opt of themeOptions; track opt.value) {
          <button
            type="button"
            role="radio"
            [attr.aria-checked]="theme.preference() === opt.value"
            [class.active]="theme.preference() === opt.value"
            (click)="theme.setPreference(opt.value)">{{ opt.label }}</button>
        }
      </div>
    </div>
    <div class="card">
      <div class="field">
        <label class="field-label" for="displayName">Visningsnamn</label>
        <input id="displayName" class="input" [(ngModel)]="displayName" />
      </div>
      <div class="field">
        <label class="field-label" for="email">E-post</label>
        <input id="email" class="input" type="email" [(ngModel)]="email" />
      </div>
      <button type="button" class="btn-primary" (click)="saveProfile()">Spara profil</button>
    </div>
    <div class="card">
      <h2 class="section-title">Byt lösenord</h2>
      <div class="field">
        <label class="field-label" for="current">Nuvarande</label>
        <input id="current" class="input" type="password" [(ngModel)]="currentPassword" />
      </div>
      <div class="field">
        <label class="field-label" for="next">Nytt</label>
        <input id="next" class="input" type="password" [(ngModel)]="newPassword" />
      </div>
      <button type="button" class="btn-secondary" (click)="changePassword()">Uppdatera lösenord</button>
    </div>
    <div class="card">
      <h2 class="section-title">Aviseringar i webbläsaren</h2>
      <p class="muted">Få push när fliken är i bakgrunden (ny förfrågan, osignerad journal, misslyckad avisering).</p>
      <button type="button" class="btn-secondary" (click)="enablePush()">Aktivera aviseringar i webbläsaren</button>
    </div>
  `,
  styles: [`
    .section-title { margin: 0 0 0.35rem; color: var(--color-primary-text); font-size: var(--text-lg); }
    .muted { margin: 0 0 0.85rem; }
  `]
})
export class AccountSettingsComponent implements OnInit {
  private api = inject(Api);
  private toast = inject(ToastService);
  private auth = inject(AuthService);
  theme = inject(ThemeService);
  readonly themeOptions: { value: ThemePreference; label: string }[] = [
    { value: 'light', label: 'Ljust' },
    { value: 'dark', label: 'Mörkt' },
    { value: 'system', label: 'Följ systemet' }
  ];
  displayName = '';
  email = '';
  currentPassword = '';
  newPassword = '';

  ngOnInit(): void {
    const user = this.auth.currentUser();
    this.displayName = user?.displayName ?? '';
    this.email = user?.email ?? '';
  }

  saveProfile(): void {
    this.api.patch('/api/app/me', { displayName: this.displayName, email: this.email }).subscribe({
      next: () => this.toast.success('Profilen sparades.'),
      error: () => this.toast.error('Kunde inte spara.')
    });
  }

  changePassword(): void {
    this.api.post('/api/app/auth/change-password', { currentPassword: this.currentPassword, newPassword: this.newPassword }).subscribe({
      next: () => {
        this.toast.success('Lösenordet uppdaterades.');
        this.currentPassword = '';
        this.newPassword = '';
      },
      error: () => this.toast.error('Kunde inte byta lösenord.')
    });
  }

  enablePush(): void {
    if (!('Notification' in window) || !('serviceWorker' in navigator)) {
      this.toast.error('Webbläsaren stödjer inte push.');
      return;
    }
    void Notification.requestPermission().then(async permission => {
      if (permission !== 'granted') {
        this.toast.error('Push nekades.');
        return;
      }
      const reg = await navigator.serviceWorker.register('/sw.js').catch(() => null);
      const push = reg?.pushManager;
      if (!push) {
        this.toast.success('Aviseringar är tillåtna i den här fliken.');
        return;
      }
      const sub = await push.subscribe({ userVisibleOnly: true }).catch(() => null);
      if (!sub) {
        this.toast.success('Aviseringar är tillåtna i den här fliken.');
        return;
      }
      const json = sub.toJSON();
      this.api.post('/api/app/notifications/push-subscription', {
        endpoint: json.endpoint,
        p256dh: json.keys?.['p256dh'],
        auth: json.keys?.['auth']
      }).subscribe({
        next: () => this.toast.success('Webbläsaraviseringar är aktiverade.'),
        error: () => this.toast.error('Kunde inte spara prenumerationen.')
      });
    });
  }
}
