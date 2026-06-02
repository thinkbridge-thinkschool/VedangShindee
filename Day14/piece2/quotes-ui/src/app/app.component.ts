import { Component, computed, inject, signal } from '@angular/core';
import { QuotesListComponent } from './quotes-list/quotes-list.component';
import { QuoteDetailComponent } from './quote-detail/quote-detail.component';
import { CreateQuoteComponent } from './create-quote/create-quote.component';
import { CreateQuoteSignalComponent } from './create-quote-signal/create-quote-signal.component';
import { QuoteEventsService } from './quote-events.service';
import { Quote } from './quote.model';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [QuotesListComponent, QuoteDetailComponent, CreateQuoteComponent, CreateQuoteSignalComponent],
  templateUrl: './app.component.html',
})
export class AppComponent {
  private readonly quoteEvents = inject(QuoteEventsService);

  selectedQuoteId = signal<number | null>(null);
  showForm        = signal(false);
  useSignalForm   = signal(true);    // default: Signal Forms (toggle still available)

  // Panel is open whenever a quote is selected OR the form is active
  isPanelOpen = computed(() => this.selectedQuoteId() !== null || this.showForm());

  onQuoteSelected(id: number): void {
    this.showForm.set(false);
    this.selectedQuoteId.set(id);
  }

  onAddQuote(): void {
    this.selectedQuoteId.set(null);
    this.showForm.set(true);
  }

  onToggleForm(): void {
    this.useSignalForm.update(v => !v);
  }

  onFormClosed(): void {
    this.showForm.set(false);
  }

  onDetailClosed(): void {
    this.selectedQuoteId.set(null);
    this.showForm.set(false);
  }

  onQuoteCreated(quote: Quote): void {
    this.quoteEvents.notifyCreated(quote);
  }
}
