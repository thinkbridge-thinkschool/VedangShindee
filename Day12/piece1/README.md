# Day 12 – Read Models & CQRS-lite

## What is CQRS?

**CQRS (Command Query Responsibility Segregation)** is a pattern that separates the write side (commands) from the read side (queries) of a feature.

- A **Command** changes state — create, update, delete.
- A **Query** reads state — it never mutates anything.

By keeping these two paths separate, each can evolve independently.

> This project uses **CQRS-lite** — no event sourcing, no separate databases. Just separate command and query paths within the same application.

---

## Write Model vs Read Model

| | Write Model | Read Model |
|---|---|---|
| Purpose | Persist and validate data | Serve data to the screen |
| Shape | Normalized (matches DB schema) | Denormalized (shaped for the UI) |
| Example here | `Quote` entity | `QuoteSummaryDto` |
| Contains | `OwnerId`, `CreatedAt`, validation rules | Only what the caller needs |

### Write Model (`Quote` entity)
The entity is normalized — it holds `OwnerId` for authorization, has length constraints enforced by the validator, and maps directly to the database table.

### Read Model (`QuoteSummaryDto`)
A projection shaped for the screen. `OwnerId` is intentionally omitted — callers don't need it, and leaking internal ownership data in every GET response is unnecessary.

---

## Command Handler

A command handler owns all write-path concerns:
1. Validate the input
2. Build the entity
3. Persist it
4. Return only what the caller needs (a result record, not the entity)

```
POST /api/quotes
    → CreateQuoteCommand (Author, Text, OwnerId)
    → CreateQuoteHandler (validate → persist)
    → CreateQuoteResult (Id, Author, Text, CreatedAt)
```

The endpoint becomes a thin translator — it extracts the user identity, builds the command, and delegates everything else to the handler.

---

## Query Handler

A query handler owns all read-path concerns:
1. Fetch from the repository
2. Project the entity into the read model
3. Return the read model

```
GET /api/quotes?page=1&size=10
    → ListQuotesQuery (Page, Size)
    → ListQuotesHandler (fetch → project)
    → List<QuoteSummaryDto>
```

The projection step (`Quote` → `QuoteSummaryDto`) is the key — it strips internal fields and shapes the response for the consumer.

---

## Why Separate Them?

**Before (mixed):** The endpoint handled validation, OwnerId extraction, persistence, and response shaping — all in one lambda.

**After (CQRS-lite):** Each path has one job:
- The command handler owns: validation + write
- The query handler owns: fetch + projection
- The endpoint owns: HTTP concerns only (auth extraction, routing, status codes)

This means:
- Adding a `FormattedDate` field to the read model only touches the query side
- Changing validation rules only touches the command handler
- Neither change affects the other

---

## Folder Structure

```
QuotesApi/
├── Commands/
│   ├── CreateQuoteCommand.cs      ← write model + result record
│   └── CreateQuoteHandler.cs      ← validate → persist → return result
├── Queries/
│   ├── QuoteSummaryDto.cs         ← read model (no OwnerId)
│   ├── ListQuotesQuery.cs         ← query + handler
│   └── GetQuoteByIdQuery.cs       ← query + handler
└── Endpoints/
    └── QuoteEndpoints.cs          ← thin translator only
```

---

## Screenshots

### 1. App Running
![Dotnet Run](Dotnet%20Run%20Screenshot.png)

`dotnet run` connects to SQL Server, seeds the database, and starts listening on `http://localhost:5051`.

### 2. JSON Response — Read Model in Action
![JSON Response](JSON%20response%20output.png)

`GET /api/quotes?page=1&size=10` returns `QuoteSummaryDto` — notice there is **no `ownerId` field** in the response. The read model only exposes what the caller needs.

### 3. Unit Tests Passing
![Unit Tests](Unit%20tests%20passing.png)

43 unit tests pass including 4 new tests for `CreateQuoteHandler` covering: validation failure, happy path, OwnerId flowing to the write model, and OwnerId absent from the result.

---

## Key Terms

| Term | Meaning |
|---|---|
| Command | An intent to change state (e.g. `CreateQuoteCommand`) |
| Query | A request for data (e.g. `ListQuotesQuery`) |
| Handler | A class that processes one command or query |
| Read Model | A DTO shaped for the consumer, not the database |
| Projection | Mapping from the write model (entity) to the read model (DTO) |
| Write Model | The normalized entity used for persistence and validation |
