import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { QuoteDetailComponent } from './quote-detail.component';
import { errorInterceptor } from '../interceptors/error.interceptor';

const SENECA = { id: 1, author: 'Seneca', text: 'Luck is preparation meeting opportunity.', createdAt: '2026-01-01T00:00:00Z' };
const MARCUS = { id: 2, author: 'Marcus Aurelius', text: 'You have power over your mind.', createdAt: '2026-01-01T00:00:00Z' };

describe('QuoteDetailComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
        provideRouter([
          { path: 'quotes/:id', component: QuoteDetailComponent },
          { path: 'quotes', component: QuoteDetailComponent },
        ]),
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('shows loading spinner while fetching', async () => {
    const harness = await RouterTestingHarness.create('/quotes/1');
    expect(harness.routeNativeElement!.textContent).toContain('Loading');
    httpMock.expectOne('/api/quotes/1').flush(SENECA);
  });

  it('renders author and text after successful fetch', async () => {
    const harness = await RouterTestingHarness.create('/quotes/1');
    httpMock.expectOne('/api/quotes/1').flush(SENECA);
    await harness.fixture.whenStable();

    const text = harness.routeNativeElement!.textContent;
    expect(text).toContain('Seneca');
    expect(text).toContain('Luck is preparation meeting opportunity.');
  });

  it('shows error + retry button on 404', async () => {
    const harness = await RouterTestingHarness.create('/quotes/999');
    httpMock.expectOne('/api/quotes/999').flush('Not Found', { status: 404, statusText: 'Not Found' });
    await harness.fixture.whenStable();

    expect(harness.routeNativeElement!.textContent).toContain('Try again');
  });

  it('loads the correct quote when navigated to a different id', async () => {
    const harness = await RouterTestingHarness.create('/quotes/2');
    httpMock.expectOne('/api/quotes/2').flush(MARCUS);
    await harness.fixture.whenStable();

    expect(harness.routeNativeElement!.textContent).toContain('Marcus Aurelius');
  });
});
