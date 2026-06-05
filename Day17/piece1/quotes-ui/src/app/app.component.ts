import { Component, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { filter } from 'rxjs';
import { CreateQuoteSignalComponent } from './create-quote-signal/create-quote-signal.component';
import { QuoteEventsService } from './quote-events.service';
import { Quote } from './quote.model';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, CreateQuoteSignalComponent],
  templateUrl: './app.component.html',
})
export class AppComponent {
  private readonly quoteEvents = inject(QuoteEventsService);
  private readonly router      = inject(Router);

  showForm   = signal(false);
  isLoggedIn = signal(!!localStorage.getItem('access_token'));

  constructor() {
    this.router.events
      .pipe(filter(e => e instanceof NavigationEnd), takeUntilDestroyed())
      .subscribe(e => {
        this.isLoggedIn.set(!!localStorage.getItem('access_token'));
        if ((e as NavigationEnd).urlAfterRedirects.startsWith('/login')) {
          this.showForm.set(false);
        }
      });
  }

  onAddQuote(): void {
    if (!this.isLoggedIn()) {
      this.router.navigate(['/login']);
      return;
    }
    this.showForm.set(true);
  }
  onFormClosed(): void { this.showForm.set(false); }

  onQuoteCreated(quote: Quote): void {
    this.quoteEvents.notifyCreated(quote);
  }

  signOut(): void {
    localStorage.removeItem('access_token');
    localStorage.removeItem('refresh_token');
    this.isLoggedIn.set(false);
    this.router.navigate(['/login']);
  }
}
