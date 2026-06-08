import { TestBed } from '@angular/core/testing';
import { HttpClient, HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { Quote } from '../quote.model';
import { ProblemDetails } from '../models/app-error.model';

const BASE = 'http://localhost:5051';

describe('API Contract — Week-1 QuotesAPI', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  // ── Test 1: GET /api/quotes shape ──────────────────────────────────────────
  describe('GET /api/quotes?page=1&size=10', () => {
    it('returns an array where each item has id(number), author(string), text(string), createdAt(string) — no invented fields', () => {
      const mockQuotes = [
        { id: 1, author: 'Seneca', text: 'Luck is preparation meeting opportunity.', createdAt: '2026-01-01T00:00:00Z' },
        { id: 2, author: 'Marcus Aurelius', text: 'You have power over your mind.', createdAt: '2026-01-02T00:00:00Z' },
      ];

      let result: Quote[] = [];
      http.get<Quote[]>(`${BASE}/api/quotes?page=1&size=10`).subscribe(data => { result = data; });

      const req = httpMock.expectOne(`${BASE}/api/quotes?page=1&size=10`);
      expect(req.request.method).toBe('GET');
      req.flush(mockQuotes);

      expect(Array.isArray(result)).toBe(true);
      expect(result.length).toBeGreaterThan(0);

      for (const q of result) {
        expect(typeof q.id).toBe('number');
        expect(typeof q.author).toBe('string');
        expect(typeof q.text).toBe('string');
        expect(typeof q.createdAt).toBe('string');

        expect((q as unknown as Record<string, unknown>)['title']).toBeUndefined();
        expect((q as unknown as Record<string, unknown>)['category']).toBeUndefined();
        expect((q as unknown as Record<string, unknown>)['name']).toBeUndefined();
      }
    });
  });

  // ── Test 2: GET /api/quotes/{id} shape ────────────────────────────────────
  describe('GET /api/quotes/{id}', () => {
    it('returns a single quote with id, author, text, createdAt on 200', () => {
      const mockQuote: Quote = {
        id: 1,
        author: 'Seneca',
        text: 'Per aspera ad astra.',
        createdAt: '2026-01-01T00:00:00Z',
      };

      let result: Quote | null = null;
      http.get<Quote>(`${BASE}/api/quotes/1`).subscribe(data => { result = data; });

      const req = httpMock.expectOne(`${BASE}/api/quotes/1`);
      expect(req.request.method).toBe('GET');
      req.flush(mockQuote);

      expect(result).not.toBeNull();
      const q = result as unknown as Quote;
      expect(typeof q.id).toBe('number');
      expect(typeof q.author).toBe('string');
      expect(typeof q.text).toBe('string');
      expect(typeof q.createdAt).toBe('string');
    });

    it('returns ProblemDetails with status=404 and title field when quote is not found', () => {
      const problemDetails: ProblemDetails = {
        type: 'https://tools.ietf.org/html/rfc7231#section-6.5.4',
        title: 'Not Found',
        status: 404,
        detail: 'Quote with id 9999 was not found.',
      };

      let httpError: HttpErrorResponse | null = null;
      http.get<Quote>(`${BASE}/api/quotes/9999`).subscribe({
        error: (err: HttpErrorResponse) => { httpError = err; },
      });

      const req = httpMock.expectOne(`${BASE}/api/quotes/9999`);
      req.flush(problemDetails, { status: 404, statusText: 'Not Found' });

      expect(httpError).not.toBeNull();
      expect(httpError!.status).toBe(404);

      const body = httpError!.error as ProblemDetails;
      expect(typeof body.title).toBe('string');
      expect(body.status).toBe(404);
    });
  });

  // ── Test 3: POST /api/quotes validation error ──────────────────────────────
  describe('POST /api/quotes — validation error', () => {
    it('returns 400 ValidationProblemDetails with errors.author and errors.text on empty body', () => {
      const validationProblem: ProblemDetails = {
        type: 'https://tools.ietf.org/html/rfc7231#section-6.5.1',
        title: 'One or more validation errors occurred.',
        status: 400,
        detail: 'See the errors property for details.',
        errors: {
          author: ['The Author field is required.'],
          text: ['The Text field is required.'],
        },
      };

      let httpError: HttpErrorResponse | null = null;
      http.post<Quote>(`${BASE}/api/quotes`, {}).subscribe({
        error: (err: HttpErrorResponse) => { httpError = err; },
      });

      const req = httpMock.expectOne(`${BASE}/api/quotes`);
      expect(req.request.method).toBe('POST');
      req.flush(validationProblem, { status: 400, statusText: 'Bad Request' });

      expect(httpError).not.toBeNull();
      expect(httpError!.status).toBe(400);

      const body = httpError!.error as ProblemDetails;
      expect(body.status).toBe(400);
      expect(body.errors).toBeDefined();
      expect(Object.prototype.hasOwnProperty.call(body.errors, 'author')).toBe(true);
      expect(Object.prototype.hasOwnProperty.call(body.errors, 'text')).toBe(true);
    });
  });

  // ── Test 4: POST /api/quotes success ──────────────────────────────────────
  describe('POST /api/quotes — success', () => {
    it('returns 201 with created quote containing id, author, text, createdAt', () => {
      const newQuote: Quote = {
        id: 42,
        author: 'Test Author',
        text: 'Test quote text',
        createdAt: '2026-06-03T00:00:00Z',
      };

      let responseStatus = 0;
      let result: Quote | null = null;

      http
        .post<Quote>(`${BASE}/api/quotes`, { author: 'Test Author', text: 'Test quote text' }, { observe: 'response' })
        .subscribe(resp => {
          responseStatus = resp.status;
          result = resp.body;
        });

      const req = httpMock.expectOne(`${BASE}/api/quotes`);
      expect(req.request.method).toBe('POST');
      req.flush(newQuote, { status: 201, statusText: 'Created' });

      expect(responseStatus).toBe(201);
      expect(result).not.toBeNull();
      const q = result as unknown as Quote;
      expect(typeof q.id).toBe('number');
      expect(typeof q.author).toBe('string');
      expect(typeof q.text).toBe('string');
      expect(typeof q.createdAt).toBe('string');
    });
  });
});
