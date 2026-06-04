# Day 16 – Piece 1: Routing, Lazy Loading, Guards

---

## Brief to Agent

```
IMPORTANT CONTEXT:
I already have a complete Angular 21 frontend with full design, styling, and components built.
DO NOT change any existing CSS or styling.
DO NOT regenerate the app from scratch.
Read my existing code and match the style.
Only create/update files needed for routing.

TASK: Add lazy-loaded routing, a functional auth guard, route params, and View Transitions
to my Angular 21 app against my real Week-1 QuotesAPI.

REAL API ENDPOINTS:
GET http://localhost:5051/api/quotes?page=1&size=10
    → returns list: [{ id, author, text, createdAt }]
GET http://localhost:5051/api/quotes/{id}
    → returns one: { id, author, text, createdAt }
    → returns 404 if not found

ROUTE PARAM: The route param is the quote 'id' (number) returned by my API.

REQUIREMENTS:
1. ROUTES — define in app.routes.ts:
   /quotes        → quotes list component (default)
   /quotes/:id    → quote detail component (LAZY loaded)
   /login         → login component
   /              → redirect to /quotes
   ** (wildcard)  → not-found component

2. LAZY LOADING — detail route MUST use loadComponent: () => import('...')

3. FUNCTIONAL AUTH GUARD — src/app/guards/auth.guard.ts
   - export const authGuard: CanActivateFn
   - Use inject() — no constructor
   - Check localStorage 'access_token'
   - If not logged in → router.parseUrl('/login')
   - Apply guard to /quotes/:id only

4. ROUTE PARAM IN DETAIL
   - inject(ActivatedRoute)
   - route.snapshot.paramMap.get('id')
   - Convert to number, redirect if invalid

5. VIEW TRANSITIONS — provideRouter(routes, withViewTransitions())

6. NAVIGATION
   - Clicking a quote navigates to /quotes/{id}
   - Detail page has a back button to /quotes

DO NOT use constructor injection, class-based guards, any type, NgModule,
or eager load the detail component.
```

---

## Agent's Route Config

### app.routes.ts
```typescript
import { Routes } from '@angular/router';
import { QuotesListComponent } from './quotes-list/quotes-list.component';
import { authGuard } from './guards/auth.guard';

export const routes: Routes = [
  { path: '', redirectTo: 'quotes', pathMatch: 'full' },
  { path: 'quotes', component: QuotesListComponent },
  {
    path: 'quotes/:id',
    loadComponent: () =>
      import('./quote-detail/quote-detail.component').then(
        (m) => m.QuoteDetailComponent
      ),
    canActivate: [authGuard],
  },
  {
    path: 'login',
    loadComponent: () =>
      import('./login/login.component').then((m) => m.LoginComponent),
  },
  {
    path: '**',
    loadComponent: () =>
      import('./not-found/not-found.component').then(
        (m) => m.NotFoundComponent
      ),
  },
];
```

---

## Agent's Auth Guard

### src/app/guards/auth.guard.ts
```typescript
import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

export const authGuard: CanActivateFn = (_route, state) => {
  const router = inject(Router);
  const token  = localStorage.getItem('access_token');
  return token
    ? true
    : router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};
```

---

## Agent's Detail Route Component

### src/app/quote-detail/quote-detail.component.ts (key parts)
```typescript
export class QuoteDetailComponent {
  private service = inject(QuotesService);
  private router  = inject(Router);
  private route   = inject(ActivatedRoute);
  readonly favs   = inject(FavoritesService);

  quoteId = signal<number | null>(null);

  constructor() {
    const raw = this.route.snapshot.paramMap.get('id');
    const n   = Number(raw);

    if (!raw || isNaN(n) || n <= 0) {
      this.router.navigate(['/quotes']);
      return;
    }

    this.quoteId.set(n);

    effect((onCleanup) => {
      const id = this.quoteId();
      this.retryTrigger();
      if (id === null) return;

      this.isDetailLoading.set(true);
      const sub = this.service.getById(id).subscribe({
        next: (quote) => { this.selectedQuote.set(quote); this.isDetailLoading.set(false); },
        error: (err)  => { this.detailError.set(err.friendlyMessage); this.isDetailLoading.set(false); },
      });
      onCleanup(() => sub.unsubscribe());
    });
  }

  onClose(): void { this.router.navigate(['/quotes']); }
}
```

---

## Verification Log

### States / Edges Exercised

| State | What I did | Result |
|---|---|---|
| **Guard redirect (unauthenticated)** | Deleted `access_token` from localStorage → visited `localhost:4200/quotes/1` | Redirected to `localhost:4200/login?returnUrl=%2Fquotes%2F1` ✅ |
| **Guard pass + returnUrl** | Logged in on login page | Redirected back to `localhost:4200/quotes/1` (not `/quotes`) ✅ |
| **Guard pass (already logged in)** | Visited `localhost:4200/quotes/203` while logged in | Detail page loaded directly, no redirect ✅ |
| **Guard redirect after sign out** | Clicked Sign Out → visited `/quotes/203` | Redirected to `login?returnUrl=%2Fquotes%2F203` ✅ |
| **Lazy chunk loading** | Cleared network log on `/quotes` → clicked a quote | New chunk `chunk-VSCOLSAC.js` downloaded, initiator `app.routes.ts:11` ✅ |
| **Lazy chunk cached** | Clicked back and opened another quote | Same chunk did NOT re-download ✅ |
| **Invalid route param** | Visited `localhost:4200/quotes/abc` | Redirected back to `/quotes` ✅ |
| **Non-existent id (404)** | Visited `localhost:4200/quotes/1` (id doesn't exist in DB) | Error message "The requested item was not found." with Try again button ✅ |

---

### Screenshots

**Guard redirect — unauthenticated:**
![Guard redirect](Guard%20redirect%20(unauthenticated).png)

**Guard pass + returnUrl working:**
![Guard pass returnUrl](Guard%20pass%20%2B%20returnUrl%20working.png)

**Guard pass with real quote:**
![Guard pass real quote](Guard%20pass%20with%20real%20quote.png)

**Lazy loading — new chunk in network tab:**
![Lazy Loading](Lazy%20Loading.png)

**Invalid/missing route param — redirected to /quotes:**
![Invalid param](Invalidmissing%20route%20param.png)

**Login required (guard blocks unauthenticated access):**
![Login Required](Login%20Required.png)

---

## One Concrete Bug the Agent Made — I Caught and Fixed

**Bug:** The agent generated the detail page's navigation button as a ✕ close button:
```html
<button class="detail-card__close" aria-label="Close quote" title="Close" (click)="onClose()">✕</button>
```

**Why it was wrong:** The task explicitly says *"Detail page has a back button to /quotes"*. A ✕ close icon is not a back button — it's semantically wrong and visually misleading for a routed page where the user navigated forward.

**Fix I made the agent apply:**
```html
<button class="detail-card__back" aria-label="Back to quotes" (click)="onClose()">← Back</button>
```

**Real Week-1 endpoint involved:** `GET /api/quotes/{id}` — the detail route loads this endpoint using the `id` field from the API response (e.g. `id: 203`). The route param is named `id` to match exactly what `GET /api/quotes` returns in each quote object.

---

## What Breaks if the API's Detail Route or id Field Changes

| Change | What breaks |
|---|---|
| API renames `id` to `quoteId` | Every `quote.id` reference in `quotes-list.component.ts` (6+ places) breaks — the list renders nothing, navigation sends `undefined` |
| API changes `/api/quotes/{id}` to `/api/quotes/detail/{id}` | `QuotesService.getById()` at `quotes.service.ts:19` breaks — detail page always shows error |
| Route param `:id` renamed in `app.routes.ts` | `paramMap.get('id')` in `quote-detail.component.ts:31` returns `null` — every detail visit redirects to `/quotes` |
| API returns `id` as string instead of number | `Number(raw)` still works but `n <= 0` check may behave differently for edge cases |
