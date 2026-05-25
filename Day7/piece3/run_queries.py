"""
Run the three set-operation queries from SQLQueryy.sql using sqlite3.
Prints formatted results for each query.
"""
import sqlite3

conn = sqlite3.connect(":memory:")
cur = conn.cursor()

# ── Schema ────────────────────────────────────────────────────
cur.executescript("""
CREATE TABLE Authors (
    AuthorId INTEGER PRIMARY KEY AUTOINCREMENT,
    Name     TEXT NOT NULL
);

CREATE TABLE Quotes (
    QuoteId   INTEGER PRIMARY KEY AUTOINCREMENT,
    AuthorId  INTEGER NOT NULL REFERENCES Authors(AuthorId),
    Category  TEXT NOT NULL,
    QuoteText TEXT NOT NULL
);

CREATE TABLE Tags (
    TagId   INTEGER PRIMARY KEY AUTOINCREMENT,
    TagName TEXT NOT NULL UNIQUE
);

CREATE TABLE QuoteTags (
    QuoteId INTEGER NOT NULL REFERENCES Quotes(QuoteId),
    TagId   INTEGER NOT NULL REFERENCES Tags(TagId),
    PRIMARY KEY (QuoteId, TagId)
);
""")

# ── Seed Authors ──────────────────────────────────────────────
cur.executemany("INSERT INTO Authors (Name) VALUES (?)", [
    ("Marcus Aurelius",),   # 1  classic + modern -> Q2 hit
    ("Seneca",),            # 2  classic + modern -> Q2 hit
    ("Oscar Wilde",),       # 3  modern only, tagged
    ("Maya Angelou",),      # 4  modern only, NO tags -> Q1 hit
    ("Friedrich Nietzsche",),  # 5  classic only, NO tags -> Q1 hit
])

# ── Seed Quotes ───────────────────────────────────────────────
cur.executemany("INSERT INTO Quotes (AuthorId, Category, QuoteText) VALUES (?,?,?)", [
    (1, "classic", "You have power over your mind, not outside events."),
    (1, "modern",  "The impediment to action advances action."),
    (2, "classic", "Luck is what happens when preparation meets opportunity."),
    (2, "modern",  "Omnia aliena sunt, tempus tantum nostrum est."),
    (3, "modern",  "Be yourself; everyone else is already taken."),
    (4, "modern",  "Nothing will work unless you do."),
    (5, "classic", "That which does not kill us makes us stronger."),
])

# ── Seed Tags ─────────────────────────────────────────────────
cur.executemany("INSERT INTO Tags (TagName) VALUES (?)", [
    ("stoicism",),
    ("wisdom",),
    ("creativity",),
    ("resilience",),
])

# ── Seed QuoteTags ────────────────────────────────────────────
cur.executemany("INSERT INTO QuoteTags VALUES (?,?)", [
    (1, 1), (1, 2),  # Marcus classic → stoicism, wisdom
    (2, 2),          # Marcus modern  → wisdom
    (3, 1), (3, 4),  # Seneca classic → stoicism, resilience
    (4, 1),          # Seneca modern  → stoicism
    (5, 3),          # Oscar modern   → creativity
    # QuoteId 6 (Maya Angelou)      → untagged
    # QuoteId 7 (Friedrich Nietzsche) → untagged
])
conn.commit()

# ── Helper ────────────────────────────────────────────────────
def run(title, operator, why, sql):
    cur.execute(sql)
    rows = cur.fetchall()
    cols = [d[0] for d in cur.description]
    print(f"\n{'='*60}")
    print(f"  {title}")
    print(f"  Operator: {operator}")
    print(f"  Why: {why}")
    print(f"{'='*60}")
    col_fmt = "  ".join(f"{c:<20}" for c in cols)
    print(col_fmt)
    print("-" * len(col_fmt))
    for row in rows:
        print("  ".join(f"{str(v):<20}" for v in row))
    print(f"  ({len(rows)} row(s))")

# ── Q1: Authors with quotes but NO tags ──────────────────────
run(
    "Q1: Authors with quotes but NO tags",
    "EXCEPT",
    "Start with all authors who have quotes, then subtract those whose quotes appear in QuoteTags.",
    """
    SELECT DISTINCT a.AuthorId, a.Name
    FROM Authors a
    INNER JOIN Quotes q ON q.AuthorId = a.AuthorId

    EXCEPT

    SELECT DISTINCT a.AuthorId, a.Name
    FROM Authors a
    INNER JOIN Quotes q     ON q.AuthorId = a.AuthorId
    INNER JOIN QuoteTags qt ON qt.QuoteId = q.QuoteId

    ORDER BY Name
    """
)

# ── Q2: Authors in BOTH 'classic' AND 'modern' ───────────────
run(
    "Q2: Authors in BOTH 'classic' AND 'modern'",
    "INTERSECT",
    "Keep only rows present in BOTH result sets — authors whose AuthorId appears in classic quotes AND in modern quotes.",
    """
    SELECT DISTINCT a.AuthorId, a.Name
    FROM Authors a
    INNER JOIN Quotes q ON q.AuthorId = a.AuthorId
    WHERE q.Category = 'classic'

    INTERSECT

    SELECT DISTINCT a.AuthorId, a.Name
    FROM Authors a
    INNER JOIN Quotes q ON q.AuthorId = a.AuthorId
    WHERE q.Category = 'modern'

    ORDER BY Name
    """
)

# ── Q3: Combined distinct tag list across classic + modern ────
run(
    "Q3: Combined distinct tag list across 'classic' + 'modern'",
    "UNION",
    "Combine all tags from classic quotes and all tags from modern quotes; UNION deduplicates automatically.",
    """
    SELECT DISTINCT t.TagName
    FROM Tags t
    INNER JOIN QuoteTags qt ON qt.TagId  = t.TagId
    INNER JOIN Quotes q     ON q.QuoteId = qt.QuoteId
    WHERE q.Category = 'classic'

    UNION

    SELECT DISTINCT t.TagName
    FROM Tags t
    INNER JOIN QuoteTags qt ON qt.TagId  = t.TagId
    INNER JOIN Quotes q     ON q.QuoteId = qt.QuoteId
    WHERE q.Category = 'modern'

    ORDER BY TagName
    """
)

conn.close()
