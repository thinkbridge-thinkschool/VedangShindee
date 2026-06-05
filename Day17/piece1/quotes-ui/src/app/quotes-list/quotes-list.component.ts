import { Component, HostListener, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { QuotesService } from '../quotes.service';
import { QuoteEventsService } from '../quote-events.service';
import { FavoritesService } from '../favorites.service';
import { Quote } from '../quote.model';
import { QuotesStore } from '../stores/quotes.store';

@Component({
  selector: 'app-quotes-list',
  standalone: true,
  templateUrl: './quotes-list.component.html',
  styleUrl: './quotes-list.component.css',
})
export class QuotesListComponent {
  private readonly store        = inject(QuotesStore);
  private readonly quotesService = inject(QuotesService);
  private readonly quoteEvents  = inject(QuoteEventsService);
  private readonly router       = inject(Router);
  readonly favs                 = inject(FavoritesService);

  // Delegate core state to the store — template calls stay identical
  readonly quotes        = this.store.quotes;
  readonly isListLoading = this.store.isLoading;
  readonly listError     = this.store.error;
  readonly currentPage   = this.store.currentPage;

  readonly skeletons = Array(6);

  // Local signals not managed by the store
  selectedId = signal<number | null>(null);
  allQuotes  = signal<Quote[]>([]);   // full dataset for client-side search
  totalCount = signal(0);
  searchTerm = signal('');
  activeTab  = signal<'all' | 'favorites'>('all');

  private readonly PAGE_SIZE = 10;  // matches store default pageSize
  private searchPage = signal(1);

  hasMore = computed(() => this.store.quotes().length === this.PAGE_SIZE);

  filteredQuotes = computed(() => {
    const term = this.searchTerm().toLowerCase().trim();
    const pool = term ? this.allQuotes() : this.store.quotes();
    const base = term
      ? pool.filter(q => q.author.toLowerCase().startsWith(term))
      : pool;
    return this.activeTab() === 'favorites'
      ? base.filter(q => this.favs.has(q.id))
      : base;
  });

  currentSearchPage = computed(() => this.searchPage());
  searchTotalPages  = computed(() =>
    Math.max(1, Math.ceil(this.filteredQuotes().length / this.PAGE_SIZE))
  );

  displayedQuotes = computed(() => {
    const term = this.searchTerm().trim();
    if (term) {
      const start = (this.searchPage() - 1) * this.PAGE_SIZE;
      return this.filteredQuotes().slice(start, start + this.PAGE_SIZE);
    }
    return this.filteredQuotes();
  });

  constructor() {
    // Full dataset for search — not part of the store's pagination
    this.quotesService.getAll().subscribe({
      next: (rows: Quote[]) => {
        this.allQuotes.set(rows);
        this.totalCount.set(rows.length);
      },
    });

    // Real-time prepend when a quote is created via the form
    this.quoteEvents.quoteCreated$
      .pipe(takeUntilDestroyed())
      .subscribe(q => {
        this.allQuotes.update(list => [q, ...list.filter(x => x.id !== q.id)]);
        this.totalCount.update(n => n + 1);
      });

    // Initial page load via the store
    this.store.loadQuotes();
  }

  @HostListener('window:keydown', ['$event'])
  onKeyDown(event: KeyboardEvent): void {
    if (event.target instanceof HTMLInputElement) return;

    const list = this.filteredQuotes();
    if (!list.length) return;

    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();
      const idx  = list.findIndex(q => q.id === this.selectedId());
      const next = event.key === 'ArrowDown'
        ? (idx < list.length - 1 ? idx + 1 : 0)
        : (idx > 0 ? idx - 1 : list.length - 1);
      this.selectedId.set(list[next].id);
    } else if (event.key === 'Enter' && this.selectedId() !== null) {
      this.router.navigate(['/quotes', this.selectedId()]);
    }
  }

  selectQuote(id: number): void { this.router.navigate(['/quotes', id]); }

  prevPage(): void {
    if (this.store.currentPage() > 1) {
      this.store.setPage(this.store.currentPage() - 1);
      this.store.loadQuotes();
    }
  }

  nextPage(): void {
    if (this.hasMore()) {
      this.store.setPage(this.store.currentPage() + 1);
      this.store.loadQuotes();
    }
  }

  randomQuote(): void {
    const list = this.filteredQuotes();
    if (!list.length) return;
    this.selectedId.set(list[Math.floor(Math.random() * list.length)].id);
  }

  onSearch(event: Event): void {
    this.searchTerm.set((event.target as HTMLInputElement).value);
    this.searchPage.set(1); // reset search page on new term
  }

  prevSearchPage(): void { this.searchPage.update(n => Math.max(1, n - 1)); }
  nextSearchPage(): void { this.searchPage.update(n => Math.min(this.searchTotalPages(), n + 1)); }

  clearSearch(): void { this.searchTerm.set(''); }

  setTab(tab: 'all' | 'favorites'): void { this.activeTab.set(tab); }

  retry(): void {
    this.store.clearError();
    this.store.setPage(1);
    this.store.loadQuotes();
  }

  truncate(text: string): string {
    return text.length > 160 ? text.slice(0, 160) + '…' : text;
  }
}
