import { Component, signal, computed, effect, inject, HostListener } from '@angular/core';
import { QuotesService } from './quotes.service';
import { Quote } from './quote.model';

@Component({
  selector: 'app-root',
  standalone: true,
  templateUrl: './app.component.html',
  styleUrl: './app.component.css',
})
export class AppComponent {
  private service = inject(QuotesService);

  // ── source signals ──────────────────────────────────────────────────────────
  transitioning = signal(false); // true during the fade-out phase
  currentPage  = signal(1);
  pageSize     = signal(10);
  searchTerm   = signal('');
  quotes       = signal<Quote[]>([]);
  isLoading    = signal(false);
  errorMessage = signal<string | null>(null);

  // ── computed ─────────────────────────────────────────────────────────────────
  filteredQuotes = computed(() =>
    this.quotes().filter(q =>
      q.author.toLowerCase().includes(this.searchTerm().toLowerCase())
    )
  );
  totalCount      = computed(() => this.filteredQuotes().length);
  paginatedQuotes = computed(() => {
    const start = (this.currentPage() - 1) * this.pageSize();
    return this.filteredQuotes().slice(start, start + this.pageSize());
  });
  totalPages  = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize())));
  pageStart   = computed(() => (this.currentPage() - 1) * this.pageSize() + 1);
  summary     = computed(() =>
    `Showing ${this.totalCount()} quotes - Page ${this.currentPage()} of ${this.totalPages()}`
  );
  hasSearch   = computed(() => this.searchTerm().length > 0);

  // ── effect ───────────────────────────────────────────────────────────────────
  constructor() {
    effect((onCleanup) => {
      const page = this.currentPage();
      const size = this.pageSize();
      console.log(`[effect] page=${page} size=${size} totalFiltered=${this.totalCount()}`);

      if (this.quotes().length > 0) return;

      this.isLoading.set(true);
      this.errorMessage.set(null);

      const sub = this.service.getAll().subscribe({
        next: (rows) => {
          this.quotes.set(rows);
          this.isLoading.set(false);
        },
        error: (err: Error) => {
          this.errorMessage.set(err.message ?? 'Failed to load quotes');
          this.isLoading.set(false);
        },
      });

      onCleanup(() => sub.unsubscribe());
    });
  }

  // ── keyboard navigation: Left arrow = Prev, Right arrow = Next ───────────────
  @HostListener('window:keydown', ['$event'])
  onKeyDown(event: KeyboardEvent): void {
    // do not fire if user is typing in the search box
    if (event.target instanceof HTMLInputElement) return;

    if (event.key === 'ArrowLeft')  this.goToPage(-1);
    if (event.key === 'ArrowRight') this.goToPage(1);
  }

  // ── event handlers ───────────────────────────────────────────────────────────
  onSearch(event: Event): void {
    this.searchTerm.set((event.target as HTMLInputElement).value);
    this.currentPage.set(1);
  }

  clearSearch(): void {
    this.searchTerm.set('');
    this.currentPage.set(1);
  }

  goToPage(delta: number): void {
    const next = this.currentPage() + delta;
    if (next < 1 || next > this.totalPages()) return;

    // fade out → change page → fade in
    this.transitioning.set(true);
    setTimeout(() => {
      this.currentPage.set(next);
      this.transitioning.set(false);
    }, 220);
  }
}
