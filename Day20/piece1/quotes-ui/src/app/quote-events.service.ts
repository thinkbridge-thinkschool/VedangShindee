import { Injectable } from '@angular/core';
import { Subject } from 'rxjs';
import { Quote } from './quote.model';

@Injectable({ providedIn: 'root' })
export class QuoteEventsService {
  readonly quoteCreated$ = new Subject<Quote>();

  notifyCreated(quote: Quote): void {
    this.quoteCreated$.next(quote);
  }
}
