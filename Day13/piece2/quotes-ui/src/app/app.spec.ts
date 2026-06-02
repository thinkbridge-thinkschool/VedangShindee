import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { AppComponent } from './app.component';

describe('AppComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        provideZonelessChangeDetection(),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(AppComponent);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('selectedQuoteId starts as null', () => {
    const fixture = TestBed.createComponent(AppComponent);
    expect(fixture.componentInstance.selectedQuoteId()).toBeNull();
  });

  it('onQuoteSelected sets selectedQuoteId', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.componentInstance.onQuoteSelected(42);
    expect(fixture.componentInstance.selectedQuoteId()).toBe(42);
  });
});
