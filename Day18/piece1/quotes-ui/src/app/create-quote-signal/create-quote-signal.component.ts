/*
SIGNAL FORMS vs REACTIVE FORMS:
Simpler: Field values are WritableSignals — no toSignal() bridge needed (zero Observable conversion).
         Validation is computed() — no statusChanges stream, no reactive boilerplate.
         Flat structure — no FormGroup nesting, no .controls accessor, no typed-form generics.
         onSubmit reads signal values directly; no null-coalescing on .value needed.
Rougher: signalForm() / signalField() are NOT shipped in Angular 21.2.x — the preview API is still
         under Angular RFC. This uses raw signal() + computed() primitives to approximate the intent.
         No built-in markAllAsTouched(), reset(), or disable() — each must be wired manually.
         Standard Validators.* don't compose here; validation is bespoke computed logic, so
         third-party validators won't plug in without adaptation.
         Template binding is more verbose: [value]="v()" + (input)="onInput($event)" vs formControlName.
*/

import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { timeout, TimeoutError } from 'rxjs';
import { QuotesService } from '../quotes.service';
import { Quote } from '../quote.model';
import { AppError } from '../models/app-error.model';

@Component({
  selector: 'app-create-quote-signal',
  standalone: true,
  imports: [],
  templateUrl: './create-quote-signal.component.html',
  styleUrl: './create-quote-signal.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CreateQuoteSignalComponent {
  private readonly quotesService = inject(QuotesService);

  readonly quoteCreated = output<Quote>();
  readonly closed       = output<void>();

  // Field values as writable signals — updated directly from (input) events
  readonly authorValue = signal('');
  readonly textValue   = signal('');

  // Per-field touched state (blur + submit path)
  private readonly _authorTouched = signal(false);
  private readonly _textTouched   = signal(false);

  // Submission / server state
  readonly isSubmitting = signal(false);
  readonly serverError  = signal<string | null>(null);
  readonly isSuccess    = signal(false);

  // Computed character counts
  readonly authorLen = computed(() => this.authorValue().length);
  readonly textLen   = computed(() => this.textValue().length);

  // Computed validation — returns the first failing rule name, or null when valid
  readonly authorError = computed<string | null>(() => {
    const v = this.authorValue();
    if (!v.trim()) return 'required';
    if (v.length > 200) return 'maxlength';
    return null;
  });

  readonly textError = computed<string | null>(() => {
    const v = this.textValue();
    if (!v.trim()) return 'required';
    if (v.length > 1000) return 'maxlength';
    return null;
  });

  // Show error only when touched AND invalid (mirrors reactive form behaviour)
  readonly authorInvalid = computed(() => this._authorTouched() && this.authorError() !== null);
  readonly textInvalid   = computed(() => this._textTouched()   && this.textError()   !== null);

  // Whole-form validity (used for submit guard; not gated on touched)
  private readonly _formInvalid = computed(
    () => this.authorError() !== null || this.textError() !== null
  );

  private readonly authorInputRef = viewChild<ElementRef<HTMLInputElement>>('authorInput');
  private readonly textInputRef   = viewChild<ElementRef<HTMLTextAreaElement>>('textInput');

  onAuthorInput(event: Event): void {
    this.authorValue.set((event.target as HTMLInputElement).value);
  }

  onTextInput(event: Event): void {
    this.textValue.set((event.target as HTMLTextAreaElement).value);
  }

  onAuthorBlur(): void { this._authorTouched.set(true); }
  onTextBlur():   void { this._textTouched.set(true);   }

  onClose(): void {
    this.authorValue.set('');
    this.textValue.set('');
    this._authorTouched.set(false);
    this._textTouched.set(false);
    this.serverError.set(null);
    this.isSuccess.set(false);
    this.closed.emit();
  }

  onSubmit(event: Event): void {
    event.preventDefault();

    // Step 1 — mark all fields touched so errors become visible
    this._authorTouched.set(true);
    this._textTouched.set(true);

    // Step 2 — focus first invalid field and bail
    if (this._formInvalid()) {
      setTimeout(() => {
        if (this.authorError() !== null) {
          this.authorInputRef()?.nativeElement.focus();
        } else {
          this.textInputRef()?.nativeElement.focus();
        }
      }, 0);
      return;
    }

    // Step 3 — enter submitting state (inputs become disabled via [disabled] binding)
    this.isSubmitting.set(true);
    this.serverError.set(null);
    this.isSuccess.set(false);

    // Step 4 — call real POST /api/quotes endpoint { author, text }
    // timeout(10000) re-enables the form if the API hangs instead of responding
    this.quotesService.createQuote(this.authorValue(), this.textValue()).pipe(timeout(10000)).subscribe({
      next: (quote: Quote) => {
        // Step 5 — success
        this.quoteCreated.emit(quote);
        this.authorValue.set('');
        this.textValue.set('');
        this._authorTouched.set(false);
        this._textTouched.set(false);
        this.isSuccess.set(true);
        this.isSubmitting.set(false);
      },
      error: (err: AppError | TimeoutError) => {
        // Step 6 — server error or timeout; re-enable form by clearing isSubmitting
        const message: string = err instanceof TimeoutError
          ? 'Request timed out. Please try again.'
          : err.friendlyMessage;
        this.serverError.set(message);
        this.isSubmitting.set(false);
      },
    });
  }
}
