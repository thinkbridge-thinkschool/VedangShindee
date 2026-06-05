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

  it('showForm starts as false', () => {
    const fixture = TestBed.createComponent(AppComponent);
    expect(fixture.componentInstance.showForm()).toBe(false);
  });

  it('onAddQuote sets showForm to true', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.componentInstance.onAddQuote();
    expect(fixture.componentInstance.showForm()).toBe(true);
  });
});
