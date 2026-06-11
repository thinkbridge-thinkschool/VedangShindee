import { Component, effect, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { QuotesService } from '../quotes.service';
import { AppError } from '../models/app-error.model';
import { FavoritesService } from '../favorites.service';
import { Quote } from '../quote.model';

@Component({
  selector: 'app-quote-detail',
  standalone: true,
  templateUrl: './quote-detail.component.html',
  styleUrl: './quote-detail.component.css',
})
export class QuoteDetailComponent {
  private service = inject(QuotesService);
  private router  = inject(Router);
  private route   = inject(ActivatedRoute);
  readonly favs   = inject(FavoritesService);

  // Populated from route param; null means invalid id → redirected in constructor
  quoteId = signal<number | null>(null);

  selectedQuote   = signal<Quote | null>(null);
  isDetailLoading = signal(false);
  detailError     = signal<string | null>(null);

  private retryTrigger = signal(0);
  copied = signal(false);

  constructor() {
    const raw = this.route.snapshot.paramMap.get('id');
    const n   = Number(raw);

    if (!raw || isNaN(n) || n <= 0) {
      this.router.navigate(['/quotes']);
      return;
    }

    this.quoteId.set(n);

    effect((onCleanup) => {
      const id = this.quoteId();
      // read retryTrigger so effect re-runs on retry
      this.retryTrigger();

      if (id === null) {
        this.selectedQuote.set(null);
        this.isDetailLoading.set(false);
        this.detailError.set(null);
        return;
      }

      this.isDetailLoading.set(true);
      this.detailError.set(null);
      this.selectedQuote.set(null);

      const sub = this.service.getById(id).subscribe({
        next: (quote: Quote) => {
          this.selectedQuote.set(quote);
          this.isDetailLoading.set(false);
        },
        error: (err: AppError) => {
          this.detailError.set(err.friendlyMessage);
          this.isDetailLoading.set(false);
        },
      });

      onCleanup(() => sub.unsubscribe());
    });
  }

  onClose(): void { this.router.navigate(['/quotes']); }

  retry(): void {
    this.retryTrigger.update((n) => n + 1);
  }

  async copyQuote(text: string, author: string): Promise<void> {
    await navigator.clipboard.writeText(`"${text}" — ${author}`);
    this.copied.set(true);
    setTimeout(() => this.copied.set(false), 2000);
  }

  formatDate(dateStr: string): string {
    return new Date(dateStr).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'long',
      day: 'numeric',
    });
  }
}
