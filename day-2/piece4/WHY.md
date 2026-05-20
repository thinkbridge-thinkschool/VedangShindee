# Why a Rich Domain Model for Quote?

The anemic `Quote` was just a bag of public properties with validation scattered across
DataAnnotations, the endpoint, and the repository. Any caller could write
`quote.Text = ""` or `new Quote { Author = "" }` and the object would silently accept
invalid state. Invariants lived nowhere in particular — they were rules the codebase
hoped developers would remember to apply, not rules the type enforced.

The rich model changes the contract: `Quote.Create(author, text)` is the only way in.
The factory checks both fields before an object even exists. Private setters mean no
code path can corrupt state after construction. `SoftDelete()` is the only mutation
allowed, making the lifecycle explicit and grep-able. EF Core receives the already-valid
object; the endpoint receives either a quote or a named `DomainError`, not an ambiguous
null or a 500.

**Scenario where the anemic version ships a bug:**
Suppose a future developer adds an admin "bulk-import" endpoint. With the anemic model
they construct quotes directly: `new Quote { Author = dto.Author, Text = dto.Text }`.
They forget that Author has a 200-char limit because that rule lived only in the
`[MaxLength]` annotation on the DTO — not on the entity. The database column accepts the
truncation silently (SQLite doesn't enforce column constraints by default), so the
overlong author name is stored, and the `GetById` response returns malformed data to
clients. With the rich model, `Quote.Create` rejects the input immediately — the bug
never reaches the database.
