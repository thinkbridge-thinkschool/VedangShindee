/*
 * WHEN TO MOVE FROM SIGNALS TO NGRX:
 *
 * Stay with signals when:
 * - State is owned and consumed by one feature (quotes, in isolation)
 * - Async flows are flat: load → success / error, with no dependent chains
 * - Team is small (≤ 3 people) sharing verbal context
 * - No requirement for time-travel debugging or Redux DevTools
 *
 * Switch to NgRx (or @ngrx/signals store) when ANY of the following is true:
 *
 * 1. SHARED STATE across two or more features — e.g. a cart badge, a
 *    notifications panel, and a summary widget all reading quotes state.
 *    Two consumers is fine; five becomes a tangle of injected services.
 *
 * 2. CHAINED ASYNC FLOWS — load user → load their quotes → load related tags.
 *    Nested subscribe chains are brittle; NgRx Effects give a composable,
 *    testable pipeline with a clear audit trail.
 *
 * 3. TIME-TRAVEL / AUDIT LOG — reproducing bugs by replaying actions, or
 *    A/B testing against recorded state sequences, requires an immutable
 *    action log that signals alone cannot provide.
 *
 * 4. TEAM SIZE > 5 touching state — the action/reducer/selector contract
 *    acts as a typed, reviewable API boundary. Signal methods are just
 *    function calls with no externally visible contract.
 *
 * 5. CROSS-FEATURE MUTATIONS — when two features each trigger state changes
 *    in the other, direct service injection becomes circular. An NgRx action
 *    is a clean broadcast any slice can independently react to.
 *
 * Practical threshold: if you need a diagram to explain the data flow to a
 * new engineer, the complexity has outgrown signals — move to NgRx.
 */

import { Injectable, signal, computed, inject } from '@angular/core';
import { Quote } from '../quote.model';
import { QuotesService } from '../quotes.service';
import { AppError } from '../models/app-error.model';

@Injectable({ providedIn: 'root' })
export class QuotesStore {
  private readonly quotesService = inject(QuotesService);

  // Private writable signals — only this service mutates them
  private readonly _quotes = signal<Quote[]>([]);
  private readonly _selectedQuote = signal<Quote | null>(null);
  private readonly _isLoading = signal<boolean>(false);
  private readonly _error = signal<string | null>(null);
  private readonly _currentPage = signal<number>(1);
  private readonly _pageSize = signal<number>(10);

  // Public readonly — components read, never write
  readonly quotes = this._quotes.asReadonly();
  readonly selectedQuote = this._selectedQuote.asReadonly();
  readonly isLoading = this._isLoading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly currentPage = this._currentPage.asReadonly();
  readonly pageSize = this._pageSize.asReadonly();

  // Derived state
  readonly totalCount = computed(() => this._quotes().length);
  readonly hasError = computed(() => this._error() !== null);
  readonly isEmpty = computed(
    () => !this._isLoading() && this._quotes().length === 0
  );

  // ── Actions ────────────────────────────────────────────────────────────────

  loadQuotes(): void {
    this._isLoading.set(true);
    this._error.set(null);

    this.quotesService
      .getPage(this._currentPage(), this._pageSize())
      .subscribe({
        next: (quotes: Quote[]) => {
          this._quotes.set(quotes);
          this._isLoading.set(false);
        },
        error: (err: AppError) => {
          this._error.set(err.friendlyMessage ?? 'Failed to load quotes.');
          this._isLoading.set(false);
        },
      });
  }

  loadQuote(id: number): void {
    this._isLoading.set(true);
    this._error.set(null);

    this.quotesService.getById(id).subscribe({
      next: (quote: Quote) => {
        this._selectedQuote.set(quote);
        this._isLoading.set(false);
      },
      error: (err: AppError) => {
        this._error.set(err.friendlyMessage ?? `Failed to load quote ${id}.`);
        this._isLoading.set(false);
      },
    });
  }

  addQuote(author: string, text: string): void {
    this._isLoading.set(true);
    this._error.set(null);

    this.quotesService.createQuote(author, text).subscribe({
      next: () => {
        // Keep isLoading true — loadQuotes will manage the flag from here
        this.loadQuotes();
      },
      error: (err: AppError) => {
        this._error.set(err.friendlyMessage ?? 'Failed to create quote.');
        this._isLoading.set(false);
      },
    });
  }

  deleteQuote(id: number): void {
    this._isLoading.set(true);
    this._error.set(null);

    this.quotesService.deleteQuote(id).subscribe({
      next: () => {
        // Optimistic removal — avoids a full list re-fetch on every delete
        this._quotes.update((list: Quote[]) =>
          list.filter((q: Quote) => q.id !== id)
        );
        this._isLoading.set(false);
      },
      error: (err: AppError) => {
        this._error.set(err.friendlyMessage ?? `Failed to delete quote ${id}.`);
        this._isLoading.set(false);
      },
    });
  }

  setPage(page: number): void {
    this._currentPage.set(page);
  }

  clearError(): void {
    this._error.set(null);
  }
}
