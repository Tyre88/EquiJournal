import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../auth.service';

@Component({
  selector: 'app-two-factor',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <div class="auth-screen">
      <div class="auth-card">
        <div class="auth-brand">
          <span class="auth-mark">H</span>
          <div>
            <h1>Tvåfaktorsverifiering</h1>
            <p class="subtitle">Ange koden från din autentiseringsapp.</p>
          </div>
        </div>

        <form [formGroup]="totpForm" (ngSubmit)="onVerify()">
          <div class="field">
            <label class="field-label" for="code">Kod</label>
            <input
              id="code"
              class="input"
              type="text"
              formControlName="code"
              placeholder="000000"
              maxlength="6"
              inputmode="numeric"
              autocomplete="one-time-code"
              style="text-align:center;letter-spacing:0.4rem;font-size:1.4rem;font-family:ui-monospace,monospace"
            />
            @if (code?.invalid && code?.touched) {
              <span class="field-error">Ange en 6-siffrig kod</span>
            }
          </div>

          @if (errorMessage()) {
            <div class="alert alert-error">{{ errorMessage() }}</div>
          }

          <button type="submit" [disabled]="totpForm.invalid || loading()" class="btn-primary" style="width:100%">
            {{ loading() ? 'Verifierar…' : 'Verifiera' }}
          </button>
        </form>

        <p class="muted" style="margin-top:1rem">
          Öppna din autentiseringsapp (t.ex. Google Authenticator) och ange koden som visas där.
        </p>
        <a routerLink="/login" class="btn-ghost" style="margin-top:0.5rem">Tillbaka till inloggning</a>
      </div>
    </div>
  `
})
export class TwoFactorComponent {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private authService = inject(AuthService);

  totpForm = this.fb.group({
    code: ['', [Validators.required, Validators.pattern('^[0-9]{6}$')]]
  });

  loading = signal(false);
  errorMessage = signal('');

  onVerify(): void {
    if (this.totpForm.invalid) return;

    this.loading.set(true);
    this.errorMessage.set('');

    const code = this.totpForm.value.code;
    if (!code) return;

    this.authService.verify2fa(code).subscribe({
      next: () => {
        this.router.navigate(['/schema']);
      },
      error: () => {
        this.errorMessage.set('Ogiltig kod. Försök igen.');
        this.loading.set(false);
      }
    });
  }

  get code() { return this.totpForm.get('code'); }
}
