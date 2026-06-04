import { Component, inject, signal } from '@angular/core';
import { NavigationEnd, NavigationStart, Router, RouterOutlet } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { filter } from 'rxjs';
import { CreateQuoteComponent } from './create-quote/create-quote.component';
import { QuoteEventsService } from './quote-events.service';
import { Quote } from './quote.model';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, CreateQuoteComponent],
  templateUrl: './app.component.html',
})
export class AppComponent {
  private readonly quoteEvents = inject(QuoteEventsService);
  private readonly router      = inject(Router);

  showForm   = signal(false);
  isLoggedIn = signal(!!localStorage.getItem('access_token'));

  constructor() {
    this.router.events
      .pipe(filter(e => e instanceof NavigationStart), takeUntilDestroyed())
      .subscribe(() => this.showForm.set(false));  // close panel on every navigation

    this.router.events
      .pipe(filter(e => e instanceof NavigationEnd), takeUntilDestroyed())
      .subscribe(() => this.isLoggedIn.set(!!localStorage.getItem('access_token')));
  }

  onAddQuote(): void { this.showForm.set(true); }
  onFormClosed(): void { this.showForm.set(false); }

  onQuoteCreated(quote: Quote): void {
    this.quoteEvents.notifyCreated(quote);
  }

  signOut(): void {
    localStorage.removeItem('access_token');
    this.isLoggedIn.set(false);
    this.router.navigate(['/login']);
  }
}
