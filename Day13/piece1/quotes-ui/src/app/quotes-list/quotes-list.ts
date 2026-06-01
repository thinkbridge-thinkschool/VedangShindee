import { Component, signal, computed, effect, inject, DestroyRef } from '@angular/core';
import { QuoteService, Quote } from '../quote.service';

@Component({
  selector: 'app-quotes-list',
  standalone: true,
  templateUrl: './quotes-list.html',
  styleUrl: './quotes-list.css',
})
export class QuotesList {
  private quoteService = inject(QuoteService);
  private destroyRef = inject(DestroyRef);

  readonly pageSizes = [5, 10, 20];

  // Source signals — user-driven state
  currentPage = signal(1);
  pageSize = signal(5);

  // Response signals — server-driven state
  quotes = signal<Quote[]>([]);
  hasMore = signal(false); // true when the API returned a full page (more pages likely exist)
  loading = signal(false);
  error = signal<string | null>(null);

  // Derived state — computed from two signals, always stays consistent
  pageStart = computed(() => (this.currentPage() - 1) * this.pageSize() + 1);
  pageEnd = computed(() => this.pageStart() + this.quotes().length - 1);
  hasQuotes = computed(() => this.quotes().length > 0);
  summary = computed(
    () =>
      `Quotes ${this.pageStart()}–${this.pageEnd()} · Page ${this.currentPage()}` +
      (this.hasMore() ? '' : ' (last page)')
  );

  constructor() {
    // effect reads currentPage + pageSize — re-runs automatically when either changes
    effect(() => {
      const page = this.currentPage();
      const size = this.pageSize();

      this.loading.set(true);
      this.error.set(null);

      const sub = this.quoteService.getPage(page, size).subscribe({
        next: (rows) => {
          this.quotes.set(rows);
          // if we got a full page, assume there are more; a partial page means last page
          this.hasMore.set(rows.length === size);
          this.loading.set(false);
        },
        error: (err: Error) => {
          this.error.set(err.message ?? 'Failed to load quotes');
          this.loading.set(false);
        },
      });

      this.destroyRef.onDestroy(() => sub.unsubscribe());
    });
  }

  goToPage(page: number): void {
    if (page < 1) return;
    if (page > this.currentPage() && !this.hasMore()) return;
    this.currentPage.set(page);
  }

  changePageSize(size: number): void {
    this.pageSize.set(size);
    this.currentPage.set(1); // reset to page 1 when page size changes
  }
}
