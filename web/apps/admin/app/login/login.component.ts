import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [ReactiveFormsModule],
  template: `
    <div class="auth-screen">
      <div class="auth-card">
        <div class="auth-brand">
          <span class="auth-mark">H</span>
          <div>
            <h1>HästJournal</h1>
            <p class="subtitle">Logga in för att fortsätta</p>
          </div>
        </div>

        <form [formGroup]="loginForm" (ngSubmit)="onLogin()">
          <div class="field">
            <label class="field-label" for="email">E-post</label>
            <input
              id="email"
              class="input"
              type="email"
              formControlName="email"
              placeholder="din@epost.se"
              autocomplete="email"
            />
            @if (email?.invalid && email?.touched) {
              <span class="field-error">Ange en giltig e-postadress</span>
            }
          </div>

          <div class="field">
            <label class="field-label" for="password">Lösenord</label>
            <input
              id="password"
              class="input"
              type="password"
              formControlName="password"
              placeholder="Ditt lösenord"
              autocomplete="current-password"
            />
            @if (password?.invalid && password?.touched) {
              <span class="field-error">Lösenord krävs</span>
            }
          </div>

          @if (errorMessage()) {
            <div class="alert alert-error">{{ errorMessage() }}</div>
          }

          <button type="submit" [disabled]="loginForm.invalid || loading()" class="btn-primary" style="width:100%">
            {{ loading() ? 'Loggar in…' : 'Logga in' }}
          </button>
        </form>
      </div>
    </div>
  `
})
export class LoginComponent {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private authService = inject(AuthService);

  loginForm = this.fb.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required]
  });

  loading = signal(false);
  errorMessage = signal('');

  onLogin(): void {
    if (this.loginForm.invalid) return;

    this.loading.set(true);
    this.errorMessage.set('');

    const { email, password } = this.loginForm.value;
    if (!email || !password) return;

    this.authService.login(email, password).subscribe({
      next: (response) => {
        if (response.requiresTwoFactor) {
          this.router.navigate(['/2fa']);
        } else {
          this.router.navigate(['/schema']);
        }
      },
      error: () => {
        this.errorMessage.set('Ogiltiga inloggningsuppgifter.');
        this.loading.set(false);
      }
    });
  }

  get email() { return this.loginForm.get('email'); }
  get password() { return this.loginForm.get('password'); }
}
