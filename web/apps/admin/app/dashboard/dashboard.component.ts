import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../auth.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="dash">
      <h1>Välkommen, {{ auth.currentUser()?.displayName || 'behandlare' }}</h1>
      <p>Skriv journaler, håll kunder och hästar uppdaterade.</p>
      <div class="cards">
        <a routerLink="/journals/new">Ny journal</a>
        <a routerLink="/journals/drafts">Osignerade utkast</a>
        <a routerLink="/owners">Kunder</a>
        <a routerLink="/horses">Hästar</a>
      </div>
    </div>
  `,
  styles: [`
    .dash { max-width: 800px; margin: 0 auto; padding: 1.5rem; }
    h1 { color: var(--color-primary-text); }
    .cards { display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; }
    a { min-height: 56px; display: flex; align-items: center; justify-content: center; background: var(--color-surface); border: 1px solid var(--color-border); border-radius: 8px; color: var(--color-primary-text); text-decoration: none; font-weight: 600; }
    @media (max-width: 640px) { .cards { grid-template-columns: 1fr; } }
  `]
})
export class DashboardComponent {
  auth = inject(AuthService);
}
