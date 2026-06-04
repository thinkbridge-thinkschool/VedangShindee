import { Component, HostListener, computed, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { QuotesService } from '../quotes.service';
import { AppError } from '../models/app-error.model';
import { QuoteEventsService } from '../quote-events.service';
import { FavoritesService } from '../favorites.service';
import { Quote } from '../quote.model';

@Component({
  selector: 'app-quotes-list',
  standalone: true,
  templateUrl: './quotes-list.component.html',
  styleUrl: './quotes-list.component.css',
})
export class QuotesListComponent {
  private quotesService = inject(QuotesService);
  private quoteEvents   = inject(QuoteEventsService);
  private router        = inject(Router);
  readonly favs         = inject(FavoritesService);

  // Internal highlight state — tracks keyboard-focused quote before navigating
  selectedId = signal<number | null>(null);

  quotes        = signal<Quote[]>([]);   // current page (server-side)
  allQuotes     = signal<Quote[]>([]);   // full dataset for search
  isListLoading = signal(false);
  listError     = signal<string | null>(null);
  hasMore       = signal(false);
  totalCount    = signal(0);
  searchTerm    = signal('');
  activeTab     = signal<'all' | 'favorites'>('all');

  private readonly PAGE_SIZE = 15;
  private page           = signal(1);   // server-side page (no search)
  private searchPage     = signal(1);   // client-side page (while searching)
  private refreshTrigger = signal(0);

  // When searching: filter allQuotes (all 337).
  // When not searching: show the current page from the server.
  filteredQuotes = computed(() => {
    const term = this.searchTerm().toLowerCase().trim();
    const pool = term ? this.allQuotes() : this.quotes();
    const base = term
      ? pool.filter(q =>
          q.author.toLowerCase().startsWith(term))
      : pool;
    const result = this.activeTab() === 'favorites'
      ? base.filter(q => this.favs.has(q.id))
      : base;

    if (term) {
      console.log(`[search] term="${term}" matched=${result.length} of ${this.allQuotes().length} total`);
    }
    return result;
  });

  currentPage       = computed(() => this.page());
  currentSearchPage = computed(() => this.searchPage());
  searchTotalPages  = computed(() =>
    Math.max(1, Math.ceil(this.filteredQuotes().length / this.PAGE_SIZE))
  );

  // What actually renders in the list
  displayedQuotes = computed(() => {
    const term = this.searchTerm().trim();
    if (term) {
      const start = (this.searchPage() - 1) * this.PAGE_SIZE;
      return this.filteredQuotes().slice(start, start + this.PAGE_SIZE);
    }
    return this.filteredQuotes(); // already a single server page
  });

  constructor() {
    // Load full dataset once — used for search + total count
    this.quotesService.getAll().subscribe({
      next: (rows: Quote[]) => {
        this.allQuotes.set(rows);
        this.totalCount.set(rows.length);
      },
    });

    // Prepend every newly created quote to the top of both lists immediately
    this.quoteEvents.quoteCreated$
      .pipe(takeUntilDestroyed())
      .subscribe(q => {
        this.allQuotes.update(list => [q, ...list.filter(x => x.id !== q.id)]);
        this.quotes.update(list => [q, ...list.filter(x => x.id !== q.id)]);
        this.totalCount.update(n => n + 1);
      });

    effect((onCleanup) => {
      const page = this.page();
      this.refreshTrigger();

      console.log(`[effect] fetching page=${page} size=${this.PAGE_SIZE}`);

      this.isListLoading.set(true);
      this.listError.set(null);

      const sub = this.quotesService.getPage(page, this.PAGE_SIZE).subscribe({
        next: (rows: Quote[]) => {
          this.quotes.set(rows);
          this.hasMore.set(rows.length === this.PAGE_SIZE);
          this.isListLoading.set(false);
          console.log(`[effect] page=${page} loaded ${rows.length} quotes, hasMore=${rows.length === this.PAGE_SIZE}`);
        },
        error: (err: AppError) => {
          this.listError.set(err.friendlyMessage);
          this.isListLoading.set(false);
        },
      });

      onCleanup(() => sub.unsubscribe());
    });
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
    if (this.page() > 1) this.page.update(n => n - 1);
  }

  nextPage(): void {
    if (this.hasMore()) this.page.update(n => n + 1);
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
    this.quotes.set([]);
    this.page.set(1);
    this.refreshTrigger.update(n => n + 1);
  }

  truncate(text: string): string {
    return text.length > 160 ? text.slice(0, 160) + '…' : text;
  }
}
