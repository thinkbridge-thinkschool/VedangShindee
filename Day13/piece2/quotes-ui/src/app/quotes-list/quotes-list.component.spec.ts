import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { QuotesListComponent } from './quotes-list.component';

const QUOTES = [
  { id: 1, author: 'Seneca', text: 'Luck is preparation meeting opportunity.', createdAt: '2026-01-01T00:00:00Z' },
  { id: 2, author: 'Marcus Aurelius', text: 'You have power over your mind.', createdAt: '2026-01-01T00:00:00Z' },
];

describe('QuotesListComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      imports: [QuotesListComponent],
      providers: [
        provideZonelessChangeDetection(),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  /** Flush the two requests the component always makes on init */
  async function flushInit(fixture: ReturnType<typeof TestBed.createComponent<QuotesListComponent>>, quotes = QUOTES) {
    await fixture.whenStable();
    httpMock.match(r => r.url.includes('size=1000')).forEach(r => r.flush(quotes));
    httpMock.match(r => r.url.includes('size=10')).forEach(r => r.flush(quotes));
    await fixture.whenStable();
  }

  it('shows loading state before API responds', async () => {
    const fixture = TestBed.createComponent(QuotesListComponent);
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('Loading');
    // clean up pending requests
    httpMock.match(() => true).forEach(r => r.flush([]));
  });

  it('renders quotes after successful load', async () => {
    const fixture = TestBed.createComponent(QuotesListComponent);
    await flushInit(fixture);
    expect(fixture.nativeElement.textContent).toContain('Seneca');
    expect(fixture.nativeElement.textContent).toContain('Marcus Aurelius');
  });

  it('shows error + retry button when page request fails', async () => {
    const fixture = TestBed.createComponent(QuotesListComponent);
    await fixture.whenStable();
    httpMock.match(r => r.url.includes('size=1000')).forEach(r => r.flush([]));
    httpMock.match(r => r.url.includes('size=10')).forEach(r =>
      r.flush('Server Error', { status: 500, statusText: 'Internal Server Error' })
    );
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('Try again');
  });

  it('shows empty state when API returns zero quotes', async () => {
    const fixture = TestBed.createComponent(QuotesListComponent);
    await flushInit(fixture, []);
    expect(fixture.nativeElement.textContent).toContain('No quotes');
  });

  it('search filters from allQuotes (full dataset), not just the current page', async () => {
    const fixture = TestBed.createComponent(QuotesListComponent);
    await flushInit(fixture);

    fixture.componentInstance.searchTerm.set('marcus');
    fixture.detectChanges();
    await fixture.whenStable();

    expect(fixture.nativeElement.textContent).toContain('Marcus Aurelius');
    expect(fixture.nativeElement.textContent).not.toContain('Seneca');
  });

  it('hides Prev/Next buttons while a search term is active', async () => {
    const fixture = TestBed.createComponent(QuotesListComponent);
    await flushInit(fixture);

    fixture.componentInstance.searchTerm.set('seneca');
    fixture.detectChanges();
    await fixture.whenStable();

    const paginationBtns = fixture.nativeElement.querySelectorAll('.pg-btn');
    expect(paginationBtns.length).toBe(0);
  });

  it('emits quoteSelected when a quote is clicked', async () => {
    const fixture = TestBed.createComponent(QuotesListComponent);
    await flushInit(fixture);

    let emitted: number | null = null;
    fixture.componentInstance.quoteSelected.subscribe((id: number) => (emitted = id));

    const firstItem = fixture.nativeElement.querySelector('.quote-item');
    firstItem.click();
    expect(emitted).toBe(1);
  });
});
