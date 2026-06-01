import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface Quote {
  id: number;
  author: string;
  text: string;
  createdAt: string;
}

@Injectable({ providedIn: 'root' })
export class QuoteService {
  private http = inject(HttpClient);

  getPage(page: number, size: number): Observable<Quote[]> {
    return this.http.get<Quote[]>(`/api/quotes?page=${page}&size=${size}`);
  }
}
