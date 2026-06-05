import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-not-found',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="nf-page">
      <div class="nf-content">
        <div class="nf-code">404</div>
        <h2 class="nf-title">Page not found</h2>
        <p class="nf-sub">The page you're looking for doesn't exist.</p>
        <a routerLink="/quotes" class="btn-add-quote">← Back to Quotes</a>
      </div>
    </div>
  `,
  styles: [`
    .nf-page {
      display: flex;
      align-items: center;
      justify-content: center;
      height: 100%;
    }
    .nf-content { text-align: center; }
    .nf-code {
      font-size: 7rem;
      font-weight: 800;
      line-height: 1;
      background: linear-gradient(135deg,#6366f1 0%,#8b5cf6 40%,#ec4899 80%);
      -webkit-background-clip: text;
      -webkit-text-fill-color: transparent;
      background-clip: text;
      margin-bottom: 1rem;
    }
    .nf-title {
      font-size: 1.5rem;
      font-weight: 700;
      color: var(--text-primary);
      margin-bottom: 0.5rem;
    }
    .nf-sub {
      color: var(--text-muted);
      font-size: 0.9rem;
      margin-bottom: 1.75rem;
    }
  `]
})
export class NotFoundComponent {}
