import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { environment } from '../environments/environment';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule],
  template: `
    <div class="page">
      <h1>Mina sidor</h1>
      <p>Ange din e-post så skickar vi en inloggningslänk.</p>
      <div class="card">
        <input class="input" type="email" [(ngModel)]="email" placeholder="namn@exempel.se" />
        <button class="btn-primary" type="button" (click)="send()">Skicka länk</button>
        @if (sent()) { <p>Om adressen finns hos oss har du fått ett mejl.</p> }
      </div>
    </div>
  `
})
export class LoginComponent {
  private http = inject(HttpClient);
  email = '';
  sent = signal(false);

  send(): void {
    this.http.post(`${environment.apiUrl}/api/public/auth/magic-link`, { email: this.email }).subscribe({
      next: () => this.sent.set(true),
      error: () => this.sent.set(true)
    });
  }
}
