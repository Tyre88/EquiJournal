import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-settings-shell',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div class="page">
      <nav class="settings-tabs" aria-label="Inställningar">
        <a routerLink="/settings/account" routerLinkActive="active">Konto</a>
        <a routerLink="/settings/practice" routerLinkActive="active">Verksamhet</a>
        <a routerLink="/settings/notifications" routerLinkActive="active">Aviseringar</a>
        <a routerLink="/settings/widget" routerLinkActive="active">Widget</a>
        <a routerLink="/settings/export" routerLinkActive="active">Exportera</a>
      </nav>
      <router-outlet />
    </div>
  `,
  styles: [`
    .settings-tabs { display: flex; gap: 0.5rem; flex-wrap: wrap; margin-bottom: 1rem; }
    .settings-tabs a { padding: 0.5rem 0.85rem; border-radius: 8px; text-decoration: none; color: var(--color-text); background: var(--color-surface); border: 1px solid var(--color-border); min-height: var(--tap-min); display: inline-flex; align-items: center; }
    .settings-tabs a.active { background: var(--color-primary-soft); border-color: var(--color-primary); color: var(--color-primary-text); }
  `]
})
export class SettingsShellComponent {}
