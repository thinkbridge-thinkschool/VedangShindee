import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { QuoteDetailComponent } from './quote-detail.component';

const SENECA = { id: 1, author: 'Seneca', text: 'Luck is preparation meeting opportunity.', createdAt: '2026-01-01T00:00:00Z' };
const MARCUS = { id: 2, author: 'Marcus Aurelius', text: 'You have power over your mind.', createdAt: '2026-01-01T00:00:00Z' };

describe('QuoteDetailComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      imports: [QuoteDetailComponent],
      providers: [
        provideZonelessChangeDetection(),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('shows placeholder when quoteId is null', async () => {
    const fixture = TestBed.createComponent(QuoteDetailComponent);
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('Select a quote');
  });

  it('shows loading spinner while fetching', async () => {
    const fixture = TestBed.createComponent(QuoteDetailComponent);
    fixture.componentRef.setInput('quoteId', 1);
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('Loading');
    httpMock.expectOne('/api/quotes/1').flush(SENECA);
  });

  it('renders author and text after successful fetch', async () => {
    const fixture = TestBed.createComponent(QuoteDetailComponent);
    fixture.componentRef.setInput('quoteId', 1);
    await fixture.whenStable();
    httpMock.expectOne('/api/quotes/1').flush(SENECA);
    await fixture.whenStable();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Seneca');
    expect(text).toContain('Luck is preparation meeting opportunity.');
  });

  it('shows error + retry button on 404', async () => {
    const fixture = TestBed.createComponent(QuoteDetailComponent);
    fixture.componentRef.setInput('quoteId', 999);
    await fixture.whenStable();
    httpMock.expectOne('/api/quotes/999').flush('Not Found', { status: 404, statusText: 'Not Found' });
    await fixture.whenStable();

    expect(fixture.nativeElement.textContent).toContain('Try again');
  });

  it('returns to placeholder when quoteId resets to null', async () => {
    const fixture = TestBed.createComponent(QuoteDetailComponent);
    fixture.componentRef.setInput('quoteId', 1);
    await fixture.whenStable();
    httpMock.expectOne('/api/quotes/1').flush(SENECA);
    await fixture.whenStable();

    fixture.componentRef.setInput('quoteId', null);
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('Select a quote');
  });

  it('clears stale quote before loading new one (race fix)', async () => {
    const fixture = TestBed.createComponent(QuoteDetailComponent);

    // Load quote 1 successfully
    fixture.componentRef.setInput('quoteId', 1);
    await fixture.whenStable();
    httpMock.expectOne('/api/quotes/1').flush(SENECA);
    await fixture.whenStable();
    expect(fixture.componentInstance.selectedQuote()?.author).toBe('Seneca');

    // Switch to quote 2 — selectedQuote must be null BEFORE the response arrives
    fixture.componentRef.setInput('quoteId', 2);
    await fixture.whenStable();
    expect(fixture.componentInstance.selectedQuote()).toBeNull();

    // Now the response arrives
    httpMock.expectOne('/api/quotes/2').flush(MARCUS);
    await fixture.whenStable();
    expect(fixture.componentInstance.selectedQuote()?.author).toBe('Marcus Aurelius');
  });
});
