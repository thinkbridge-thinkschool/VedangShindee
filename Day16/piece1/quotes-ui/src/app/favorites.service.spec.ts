import { TestBed } from '@angular/core/testing';
import { FavoritesService } from './favorites.service';

describe('FavoritesService', () => {
  let service: FavoritesService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({});
    service = TestBed.inject(FavoritesService);
  });

  it('starts with no favorites', () => {
    expect(service.favorites().size).toBe(0);
    expect(service.has(1)).toBe(false);
  });

  it('toggle adds a quote to starred', () => {
    service.toggle(1);
    expect(service.has(1)).toBe(true);
    expect(service.favorites().size).toBe(1);
  });

  it('toggle called twice removes the quote', () => {
    service.toggle(1);
    service.toggle(1);
    expect(service.has(1)).toBe(false);
    expect(service.favorites().size).toBe(0);
  });

  it('has() returns false for an id never toggled', () => {
    expect(service.has(999)).toBe(false);
  });

  it('persists to localStorage after toggle', () => {
    service.toggle(42);
    const stored = JSON.parse(localStorage.getItem('quote-favorites') ?? '[]') as number[];
    expect(stored).toContain(42);
  });

  it('removes from localStorage after un-toggle', () => {
    service.toggle(42);
    service.toggle(42);
    const stored = JSON.parse(localStorage.getItem('quote-favorites') ?? '[]') as number[];
    expect(stored).not.toContain(42);
  });

  it('loads pre-existing favorites from localStorage on init', () => {
    localStorage.setItem('quote-favorites', JSON.stringify([7, 8]));
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({});
    const fresh = TestBed.inject(FavoritesService);
    expect(fresh.has(7)).toBe(true);
    expect(fresh.has(8)).toBe(true);
    expect(fresh.favorites().size).toBe(2);
  });
});
