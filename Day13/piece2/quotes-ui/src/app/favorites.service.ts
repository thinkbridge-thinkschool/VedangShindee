import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class FavoritesService {
  private readonly KEY = 'quote-favorites';

  favorites = signal<Set<number>>(
    new Set(JSON.parse(localStorage.getItem(this.KEY) ?? '[]') as number[])
  );

  toggle(id: number): void {
    const next = new Set(this.favorites());
    if (next.has(id)) next.delete(id);
    else next.add(id);
    this.favorites.set(next);
    localStorage.setItem(this.KEY, JSON.stringify([...next]));
  }

  has(id: number): boolean {
    return this.favorites().has(id);
  }
}
