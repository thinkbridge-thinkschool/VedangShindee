-- ============================================================
-- Setup: Create QuotesDb with normalized schema for set-ops
-- Tables: Authors, Quotes (with Category), Tags, QuoteTags
-- Run once before SQLQueryy.sql
-- ============================================================

USE master;
GO

IF DB_ID('QuotesDb') IS NOT NULL
BEGIN
    ALTER DATABASE QuotesDb SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE QuotesDb;
END
GO

CREATE DATABASE QuotesDb;
GO

USE QuotesDb;
GO

-- ── Authors ──────────────────────────────────────────────────
CREATE TABLE Authors (
    AuthorId INT IDENTITY(1,1) PRIMARY KEY,
    Name     NVARCHAR(100) NOT NULL
);
GO

-- ── Quotes ───────────────────────────────────────────────────
CREATE TABLE Quotes (
    QuoteId   INT IDENTITY(1,1) PRIMARY KEY,
    AuthorId  INT           NOT NULL REFERENCES Authors(AuthorId),
    Category  NVARCHAR(50)  NOT NULL,   -- 'classic' | 'modern'
    QuoteText NVARCHAR(500) NOT NULL,
    CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
);
GO

-- ── Tags ─────────────────────────────────────────────────────
CREATE TABLE Tags (
    TagId   INT IDENTITY(1,1) PRIMARY KEY,
    TagName NVARCHAR(50) NOT NULL UNIQUE
);
GO

-- ── QuoteTags (junction) ─────────────────────────────────────
CREATE TABLE QuoteTags (
    QuoteId INT NOT NULL REFERENCES Quotes(QuoteId),
    TagId   INT NOT NULL REFERENCES Tags(TagId),
    PRIMARY KEY (QuoteId, TagId)
);
GO

-- ── Seed Authors (5) ─────────────────────────────────────────
INSERT INTO Authors (Name) VALUES
    ('Marcus Aurelius'),   -- 1  classic + modern → Q2 hit
    ('Seneca'),            -- 2  classic + modern → Q2 hit
    ('Oscar Wilde'),       -- 3  modern only, tagged
    ('Maya Angelou'),      -- 4  modern only, NO tags → Q1 hit
    ('Friedrich Nietzsche');  -- 5  classic only, NO tags → Q1 hit
GO

-- ── Seed Quotes ──────────────────────────────────────────────
INSERT INTO Quotes (AuthorId, Category, QuoteText) VALUES
-- Marcus Aurelius: one classic, one modern  → appears in Q2
(1, 'classic', 'You have power over your mind, not outside events.'),          -- QuoteId 1
(1, 'modern',  'The impediment to action advances action.'),                    -- QuoteId 2

-- Seneca: one classic, one modern           → appears in Q2
(2, 'classic', 'Luck is what happens when preparation meets opportunity.'),    -- QuoteId 3
(2, 'modern',  'Omnia aliena sunt, tempus tantum nostrum est.'),               -- QuoteId 4

-- Oscar Wilde: modern only, tagged
(3, 'modern',  'Be yourself; everyone else is already taken.'),                -- QuoteId 5

-- Maya Angelou: modern only, NO tags        → appears in Q1
(4, 'modern',  'Nothing will work unless you do.'),                            -- QuoteId 6

-- Friedrich Nietzsche: classic only, NO tags → appears in Q1
(5, 'classic', 'That which does not kill us makes us stronger.');              -- QuoteId 7
GO

-- ── Seed Tags (4) ────────────────────────────────────────────
INSERT INTO Tags (TagName) VALUES
    ('stoicism'),     -- 1
    ('wisdom'),       -- 2
    ('creativity'),   -- 3
    ('resilience');   -- 4
GO

-- ── Seed QuoteTags ───────────────────────────────────────────
-- Marcus classic  → stoicism, wisdom
INSERT INTO QuoteTags VALUES (1, 1), (1, 2);
-- Marcus modern   → wisdom
INSERT INTO QuoteTags VALUES (2, 2);
-- Seneca classic  → stoicism, resilience
INSERT INTO QuoteTags VALUES (3, 1), (3, 4);
-- Seneca modern   → stoicism
INSERT INTO QuoteTags VALUES (4, 1);
-- Oscar modern    → creativity
INSERT INTO QuoteTags VALUES (5, 3);
-- QuoteId 6 (Maya)       → intentionally untagged
-- QuoteId 7 (Nietzsche)  → intentionally untagged
GO

SELECT 'Setup complete: '
     + CAST((SELECT COUNT(*) FROM Authors) AS VARCHAR) + ' authors, '
     + CAST((SELECT COUNT(*) FROM Quotes)  AS VARCHAR) + ' quotes, '
     + CAST((SELECT COUNT(*) FROM Tags)    AS VARCHAR) + ' tags, '
     + CAST((SELECT COUNT(*) FROM QuoteTags) AS VARCHAR) + ' quote-tag links.'
  AS SetupStatus;
GO
