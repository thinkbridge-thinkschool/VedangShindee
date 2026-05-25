-- ============================================================
-- Setup: Create QuotesDb and seed sample data
-- Run this once before assignment-quotes-cte.sql
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

CREATE TABLE Quotes (
    Id        INT IDENTITY(1,1)   PRIMARY KEY,
    Author    NVARCHAR(100)        NOT NULL,
    Text      NVARCHAR(1000)       NOT NULL,
    CreatedAt DATETIMEOFFSET       NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    IsDeleted BIT                  NOT NULL DEFAULT 0,
    OwnerId   INT                  NULL
);
GO

-- Seed data: 10 authors, 26 quotes total
INSERT INTO Quotes (Author, Text, CreatedAt, IsDeleted) VALUES

-- Marcus Aurelius (3 quotes)
('Marcus Aurelius', 'You have power over your mind, not outside events. Realize this, and you will find strength.',   '2024-01-10 09:00:00 +00:00', 0),
('Marcus Aurelius', 'The impediment to action advances action. What stands in the way becomes the way.',              '2024-02-14 10:30:00 +00:00', 0),
('Marcus Aurelius', 'Waste no more time arguing about what a good man should be. Be one.',                            '2024-03-20 08:15:00 +00:00', 0),

-- Seneca (2 quotes)
('Seneca', 'Luck is what happens when preparation meets opportunity.',                                                '2024-01-05 11:00:00 +00:00', 0),
('Seneca', 'Omnia aliena sunt, tempus tantum nostrum est.',                                                           '2024-04-01 07:00:00 +00:00', 0),

-- Epictetus (4 quotes)
('Epictetus', 'It is not what happens to you, but how you react to it that matters.',                                 '2024-01-01 06:00:00 +00:00', 0),
('Epictetus', 'Make the best use of what is in your power, and take the rest as it happens.',                         '2024-02-28 14:00:00 +00:00', 0),
('Epictetus', 'He is a wise man who does not grieve for the things which he has not.',                                '2024-03-15 12:00:00 +00:00', 0),
('Epictetus', 'First say to yourself what you would be; then do what you have to do.',                                '2024-04-10 09:00:00 +00:00', 0),

-- Albert Camus (2 quotes)
('Albert Camus', 'You will never be happy if you continue to search for what happiness consists of.',                  '2024-01-20 15:00:00 +00:00', 0),
('Albert Camus', 'In the middle of difficulty lies opportunity.',                                                      '2024-03-05 11:30:00 +00:00', 0),

-- Friedrich Nietzsche (2 quotes)
('Friedrich Nietzsche', 'That which does not kill us makes us stronger.',                                              '2024-01-15 10:00:00 +00:00', 0),
('Friedrich Nietzsche', 'Without music, life would be a mistake.',                                                     '2024-02-20 09:00:00 +00:00', 0),

-- Rumi (3 quotes)
('Rumi', 'Out beyond ideas of wrongdoing and rightdoing, there is a field. I will meet you there.',                    '2024-01-25 08:00:00 +00:00', 0),
('Rumi', 'Yesterday I was clever, so I wanted to change the world. Today I am wise, so I am changing myself.',         '2024-02-10 16:00:00 +00:00', 0),
('Rumi', 'The wound is the place where the Light enters you.',                                                         '2024-04-05 13:00:00 +00:00', 0),

-- Maya Angelou (2 quotes)
('Maya Angelou', 'I have learned that people will forget what you said, what you did, but never how you made them feel.', '2024-02-01 10:00:00 +00:00', 0),
('Maya Angelou', 'Nothing will work unless you do.',                                                                   '2024-03-25 14:30:00 +00:00', 0),

-- Oscar Wilde (3 quotes)
('Oscar Wilde', 'Be yourself; everyone else is already taken.',                                                        '2024-01-08 11:00:00 +00:00', 0),
('Oscar Wilde', 'We are all in the gutter, but some of us are looking at the stars.',                                  '2024-02-22 09:30:00 +00:00', 0),
('Oscar Wilde', 'I can resist everything except temptation.',                                                          '2024-03-18 15:00:00 +00:00', 0),

-- Winston Churchill (2 quotes)
('Winston Churchill', 'Success is not final; failure is not fatal: it is the courage to continue that counts.',         '2024-01-30 08:00:00 +00:00', 0),
('Winston Churchill', 'If you are going through hell, keep going.',                                                    '2024-03-10 10:00:00 +00:00', 0),

-- Mark Twain (3 quotes)
('Mark Twain', 'The secret of getting ahead is getting started.',                                                      '2024-01-12 13:00:00 +00:00', 0),
('Mark Twain', 'Courage is resistance to fear, mastery of fear, not absence of fear.',                                 '2024-02-18 11:00:00 +00:00', 0),
('Mark Twain', 'Truth is stranger than fiction, but it is because fiction is obliged to stick to possibilities.',      '2024-04-15 10:00:00 +00:00', 0);
GO

SELECT 'Seeded ' + CAST(COUNT(*) AS VARCHAR) + ' quotes across '
     + CAST(COUNT(DISTINCT Author) AS VARCHAR) + ' authors.' AS SetupStatus
FROM Quotes;
GO
