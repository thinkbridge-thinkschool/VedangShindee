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
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { QuotesService } from '../quotes.service';
import { Quote } from '../quote.model';

interface ApiError {
  title?: string;
  detail?: string;
}

@Component({
  selector: 'app-create-quote',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './create-quote.component.html',
  styleUrl: './create-quote.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CreateQuoteComponent {
  private readonly fb             = inject(FormBuilder);
  private readonly quotesService  = inject(QuotesService);

  readonly quoteCreated = output<Quote>();
  readonly closed       = output<void>();

  readonly isSubmitting = signal(false);
  readonly serverError  = signal<string | null>(null);
  readonly isSuccess    = signal(false);

  // Track per-field touched state reactively (blur + submit path)
  private readonly _authorTouched = signal(false);
  private readonly _textTouched   = signal(false);

  readonly form = this.fb.group({
    author: ['', [Validators.required, Validators.maxLength(200)]],
    text:   ['', [Validators.required, Validators.maxLength(1000)]],
  });

  get authorCtrl() { return this.form.controls.author; }
  get textCtrl()   { return this.form.controls.text; }

  // Convert form observables → signals so zoneless CD tracks them
  private readonly _authorValue  = toSignal(this.authorCtrl.valueChanges,  { initialValue: this.authorCtrl.value  ?? '' });
  private readonly _textValue    = toSignal(this.textCtrl.valueChanges,    { initialValue: this.textCtrl.value    ?? '' });
  private readonly _authorStatus = toSignal(this.authorCtrl.statusChanges, { initialValue: this.authorCtrl.status });
  private readonly _textStatus   = toSignal(this.textCtrl.statusChanges,   { initialValue: this.textCtrl.status   });

  readonly authorLen = computed(() => this._authorValue()?.length ?? 0);
  readonly textLen   = computed(() => this._textValue()?.length   ?? 0);

  readonly authorInvalid = computed(() => {
    const status = this._authorStatus();
    return this._authorTouched() && status === 'INVALID';
  });

  readonly textInvalid = computed(() => {
    const status = this._textStatus();
    return this._textTouched() && status === 'INVALID';
  });

  private readonly authorInputRef = viewChild<ElementRef<HTMLInputElement>>('authorInput');
  private readonly textInputRef   = viewChild<ElementRef<HTMLTextAreaElement>>('textInput');

  onAuthorBlur(): void { this._authorTouched.set(true); }
  onTextBlur():   void { this._textTouched.set(true);   }

  onClose(): void {
    this.form.reset();
    this._authorTouched.set(false);
    this._textTouched.set(false);
    this.serverError.set(null);
    this.isSuccess.set(false);
    this.closed.emit();
  }

  onSubmit(): void {
    // Step 1 — mark all touched
    this._authorTouched.set(true);
    this._textTouched.set(true);
    this.form.markAllAsTouched();

    // Step 2 — focus first invalid field and stop
    if (this.form.invalid) {
      // setTimeout ensures the DOM re-renders with errors before focus so
      // screen readers can announce the label + error via aria-describedby
      setTimeout(() => {
        if (this.authorCtrl.invalid) {
          this.authorInputRef()?.nativeElement.focus();
        } else {
          this.textInputRef()?.nativeElement.focus();
        }
      }, 0);
      return;
    }

    const author = this.authorCtrl.value ?? '';
    const text   = this.textCtrl.value   ?? '';

    // Step 3
    this.isSubmitting.set(true);
    this.serverError.set(null);
    this.isSuccess.set(false);
    this.form.disable();

    // Step 4
    this.quotesService.createQuote(author, text).subscribe({
      next: (quote: Quote) => {
        // Step 5
        this.quoteCreated.emit(quote);
        this.form.enable();
        this.form.reset();
        this._authorTouched.set(false);
        this._textTouched.set(false);
        this.isSuccess.set(true);
        // Step 7
        this.isSubmitting.set(false);
      },
      error: (err: HttpErrorResponse) => {
        // Step 6
        const body = err.error as ApiError | null;
        const message: string =
          body?.title ??
          body?.detail ??
          err.message ??
          'Failed to create quote. Please try again.';
        this.serverError.set(message);
        this.form.enable();
        // Step 7
        this.isSubmitting.set(false);
      },
    });
  }
}
