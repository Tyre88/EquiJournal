import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../auth.service';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <div class="auth-screen">
      <div class="auth-card">
        <div class="auth-brand">
          <span class="auth-mark">H</span>
          <div>
            <h1>Skapa verksamhet</h1>
            <p class="subtitle">HästJournal är gratis att komma igång</p>
          </div>
        </div>

        <form [formGroup]="form" (ngSubmit)="onSubmit()">
          <div class="field">
            <label class="field-label" for="name">Verksamhetens namn</label>
            <input id="name" class="input" type="text" formControlName="name" autocomplete="organization" />
          </div>
          <div class="field">
            <label class="field-label" for="displayName">Ditt namn</label>
            <input id="displayName" class="input" type="text" formControlName="displayName" autocomplete="name" />
          </div>
          <div class="field">
            <label class="field-label" for="email">E-post</label>
            <input id="email" class="input" type="email" formControlName="email" autocomplete="email" />
          </div>
          <div class="field">
            <label class="field-label" for="password">Lösenord</label>
            <input id="password" class="input" type="password" formControlName="password" autocomplete="new-password" />
            <span class="field-hint">Minst 12 tecken med stor/liten bokstav, siffra och symbol.</span>
          </div>

          @if (errorMessage()) {
            <div class="alert alert-error">{{ errorMessage() }}</div>
          }

          <button type="submit" [disabled]="form.invalid || loading()" class="btn-primary" style="width:100%">
            {{ loading() ? 'Skapar…' : 'Skapa konto' }}
          </button>
        </form>
        <p class="subtitle" style="margin-top:1rem">
          Har du redan konto? <a routerLink="/login">Logga in</a>
        </p>
      </div>
    </div>
  `
})
export class RegisterComponent {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private auth = inject(AuthService);

  form = this.fb.group({
    name: ['', [Validators.required, Validators.minLength(2)]],
    displayName: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(12)]]
  });

  loading = signal(false);
  errorMessage = signal('');

  onSubmit(): void {
    if (this.form.invalid) return;
    this.loading.set(true);
    this.errorMessage.set('');
    const { name, displayName, email, password } = this.form.getRawValue();
    this.auth.register({
      name: name ?? '',
      displayName: displayName ?? '',
      email: email ?? '',
      password: password ?? ''
    }).subscribe({
      next: () => this.router.navigate(['/schema']),
      error: err => {
        this.errorMessage.set(err?.error?.error ?? 'Kunde inte skapa verksamheten.');
        this.loading.set(false);
      }
    });
  }
}
