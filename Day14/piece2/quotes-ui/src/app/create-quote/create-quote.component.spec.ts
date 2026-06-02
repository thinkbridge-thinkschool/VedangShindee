import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { CreateQuoteComponent } from './create-quote.component';

describe('CreateQuoteComponent', () => {
  let component: CreateQuoteComponent;
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CreateQuoteComponent],
      providers: [
        provideZonelessChangeDetection(),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(CreateQuoteComponent);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  // ── State: Empty ──────────────────────────────────────────────────
  it('creates the component', () => {
    expect(component).toBeTruthy();
  });

  it('form starts invalid with no errors shown (untouched)', () => {
    expect(component.form.invalid).toBe(true);
    expect(component.authorInvalid()).toBe(false);
    expect(component.textInvalid()).toBe(false);
  });

  // ── State: Invalid (touched + invalid) ───────────────────────────
  it('submit with empty fields marks both controls touched and shows errors', () => {
    component.onSubmit();
    http.expectNone('/api/quotes');
    expect(component.authorCtrl.touched).toBe(true);
    expect(component.textCtrl.touched).toBe(true);
  });

  it('author validator rejects strings over 200 chars', () => {
    component.authorCtrl.setValue('A'.repeat(201));
    expect(component.authorCtrl.hasError('maxlength')).toBe(true);
  });

  it('text validator rejects strings over 1000 chars', () => {
    component.textCtrl.setValue('A'.repeat(1001));
    expect(component.textCtrl.hasError('maxlength')).toBe(true);
  });

  it('author validator accepts exactly 200 chars', () => {
    component.authorCtrl.setValue('A'.repeat(200));
    expect(component.authorCtrl.valid).toBe(true);
  });

  it('text validator accepts exactly 1000 chars', () => {
    component.textCtrl.setValue('A'.repeat(1000));
    expect(component.textCtrl.valid).toBe(true);
  });

  // ── State: Submitting ─────────────────────────────────────────────
  it('valid submit sends POST with correct body and sets isSubmitting', () => {
    component.authorCtrl.setValue('Vedang');
    component.textCtrl.setValue('Hello world');
    component.onSubmit();

    expect(component.isSubmitting()).toBe(true);

    const req = http.expectOne('/api/quotes');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ author: 'Vedang', text: 'Hello world' });
    req.flush(
      { id: 99, author: 'Vedang', text: 'Hello world', createdAt: '2026-06-02T00:00:00Z' },
      { status: 201, statusText: 'Created' }
    );
  });

  // ── State: Success ────────────────────────────────────────────────
  it('successful submit sets isSuccess, clears isSubmitting, and resets form', () => {
    component.authorCtrl.setValue('Vedang');
    component.textCtrl.setValue('Hello world');
    component.onSubmit();

    http.expectOne('/api/quotes').flush(
      { id: 99, author: 'Vedang', text: 'Hello world', createdAt: '2026-06-02T00:00:00Z' },
      { status: 201, statusText: 'Created' }
    );

    expect(component.isSuccess()).toBe(true);
    expect(component.isSubmitting()).toBe(false);
    expect(component.form.value.author).toBeFalsy();
    expect(component.form.value.text).toBeFalsy();
  });

  // ── State: Server error ───────────────────────────────────────────
  it('server error sets serverError signal, clears isSubmitting, and re-enables form', () => {
    component.authorCtrl.setValue('Vedang');
    component.textCtrl.setValue('Hello world');
    component.onSubmit();

    http.expectOne('/api/quotes').flush(
      { title: 'Validation failed' },
      { status: 400, statusText: 'Bad Request' }
    );

    expect(component.serverError()).toBe('Validation failed');
    expect(component.isSubmitting()).toBe(false);
    expect(component.form.enabled).toBe(true);
  });

  it('form re-enables after server error so user can retry', () => {
    component.authorCtrl.setValue('Vedang');
    component.textCtrl.setValue('Hello world');
    component.onSubmit();

    http.expectOne('/api/quotes').flush(
      { title: 'Server error' },
      { status: 500, statusText: 'Internal Server Error' }
    );

    expect(component.form.disabled).toBe(false);
  });
});
