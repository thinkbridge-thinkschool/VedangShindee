import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="login-page">
      <div class="login-card">
        <div class="login-card__mark">❝</div>
        <h2 class="login-card__title">Welcome back</h2>
        <p class="login-card__sub">Sign in to explore quotes</p>
        <form class="login-form" (submit)="login($event)">
          <div class="input-wrap">
            <input #emailInput class="login-input" [class.login-input--error]="emailError()"
                   type="text" placeholder="Email"
                   (input)="onEmailInput($event)" required />
            @if (emailError()) {
              <span class="input-error">{{ emailError() }}</span>
            }
          </div>
          <input #passwordInput class="login-input" type="password" placeholder="Password" required />
          @if (loginError()) {
            <span class="input-error">{{ loginError() }}</span>
          }
          <button class="login-btn" type="submit" [disabled]="isLoading()">
            {{ isLoading() ? 'Signing in…' : 'Sign In' }}
          </button>
        </form>
        <a class="login-guest" routerLink="/quotes">Browse quotes without signing in →</a>
        <div class="demo-hint">
          <span class="demo-hint__label">Demo credentials</span>
          <span class="demo-hint__row">test&#64;example.com</span>
          <span class="demo-hint__row">password123</span>
        </div>
      </div>
    </div>
  `,
  styles: [`
    :host {
      display: flex;
      flex: 1;
      height: 100%;
    }
    .login-page {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 100%;
      background: var(--bg);
    }
    .login-card {
      background: linear-gradient(180deg, #0d1526 0%, #0a1020 100%);
      border: 1px solid rgba(99,102,241,0.15);
      border-radius: var(--radius-lg);
      padding: 3rem 2.5rem;
      width: 100%;
      max-width: 380px;
      text-align: center;
      box-shadow: 0 8px 40px rgba(0,0,0,0.5);
    }
    .login-card__mark {
      font-size: 3.5rem;
      background: linear-gradient(135deg,#6366f1,#8b5cf6,#ec4899);
      -webkit-background-clip: text;
      -webkit-text-fill-color: transparent;
      background-clip: text;
      opacity: 0.6;
      font-family: Georgia, serif;
      line-height: 1;
      margin-bottom: 1rem;
    }
    .login-card__title {
      font-size: 1.5rem;
      font-weight: 700;
      color: var(--text-primary);
      margin-bottom: 0.4rem;
    }
    .login-card__sub {
      color: var(--text-muted);
      font-size: 0.875rem;
      margin-bottom: 1.75rem;
    }
    .login-form {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }
    .input-wrap {
      display: flex;
      flex-direction: column;
      gap: 0.3rem;
      text-align: left;
    }
    .login-input {
      background: var(--surface-2);
      border: 1px solid var(--border);
      border-radius: var(--radius);
      color: var(--text-primary);
      font-family: inherit;
      font-size: 0.9rem;
      padding: 0.65rem 1rem;
      outline: none;
      transition: border-color 0.18s;
      width: 100%;
    }
    .login-input:focus { border-color: var(--accent); }
    .login-input::placeholder { color: var(--text-muted); }
    .login-input--error { border-color: var(--error) !important; }
    .input-error {
      font-size: 0.75rem;
      color: var(--error);
      padding-left: 0.25rem;
    }
    .login-btn {
      background: linear-gradient(135deg, #4f46e5, #7c3aed);
      border: none;
      border-radius: var(--radius);
      color: #fff;
      cursor: pointer;
      font-family: inherit;
      font-size: 0.9rem;
      font-weight: 600;
      padding: 0.72rem;
      margin-top: 0.5rem;
      transition: opacity 0.18s, transform 0.15s;
    }
    .login-btn:hover { opacity: 0.9; transform: translateY(-1px); }
    .login-btn:active { transform: translateY(0); }
    .login-guest {
      display: block;
      margin-top: 1.25rem;
      color: var(--text-muted);
      font-size: 0.8rem;
      text-decoration: none;
      transition: color 0.18s;
    }
    .login-guest:hover { color: var(--accent-soft); }
    .demo-hint {
      margin-top: 1rem;
      padding: 0.65rem 1rem;
      background: rgba(99,102,241,0.08);
      border: 1px dashed rgba(99,102,241,0.25);
      border-radius: var(--radius);
      display: flex;
      flex-direction: column;
      gap: 0.2rem;
    }
    .demo-hint__label {
      font-size: 0.7rem;
      color: var(--text-muted);
      text-transform: uppercase;
      letter-spacing: 0.05em;
      margin-bottom: 0.2rem;
    }
    .demo-hint__row {
      font-size: 0.82rem;
      color: var(--accent-soft);
      font-family: monospace;
    }
  `]
})
export class LoginComponent {
  private readonly router = inject(Router);
  private readonly route  = inject(ActivatedRoute);
  private readonly http   = inject(HttpClient);

  emailError = signal<string | null>(null);
  loginError = signal<string | null>(null);
  isLoading  = signal(false);

  onEmailInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    if (!value) { this.emailError.set(null); return; }
    this.emailError.set(
      value.includes('@') && value.includes('.') ? null : 'Enter a valid email address'
    );
  }

  login(event: Event): void {
    event.preventDefault();
    const form     = event.target as HTMLFormElement;
    const email    = (form.elements[0] as HTMLInputElement).value.trim();
    const password = (form.elements[1] as HTMLInputElement).value;

    if (!email.includes('@') || !email.includes('.')) {
      this.emailError.set('Enter a valid email address');
      return;
    }

    this.emailError.set(null);
    this.loginError.set(null);
    this.isLoading.set(true);

    this.http.post<{ access_token: string; refresh_token: string; expires_in: number }>('/api/auth/login', { email, password })
      .subscribe({
        next: (res) => {
          localStorage.setItem('access_token', res.access_token);
          localStorage.setItem('refresh_token', res.refresh_token);
          this.isLoading.set(false);
          const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/quotes';
          this.router.navigateByUrl(returnUrl);
        },
        error: () => {
          this.loginError.set('Invalid email or password.');
          this.isLoading.set(false);
        },
      });
  }
}
