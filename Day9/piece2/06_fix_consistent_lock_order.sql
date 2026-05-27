USE DeadlockDemo;
GO


BEGIN TRANSACTION;

    UPDATE AccountA SET Balance = Balance - 100 WHERE Id = 1;  -- lock A
    UPDATE AccountB SET Balance = Balance + 100 WHERE Id = 1;  -- lock B

COMMIT TRANSACTION;
GO

-- -----------------------------------------------
-- Fixed Session 2  (REORDERED: now A→B, not B→A)
-- -----------------------------------------------
BEGIN TRANSACTION;

    -- Acquire AccountA FIRST (same order as Session 1)
    UPDATE AccountA SET Balance = Balance + 200 WHERE Id = 1;  -- lock A
    UPDATE AccountB SET Balance = Balance - 200 WHERE Id = 1;  -- lock B

COMMIT TRANSACTION;
GO

-- -----------------------------------------------
-- Verify final balances
-- -----------------------------------------------
SELECT 'AccountA' AS [Table], Id, Balance FROM AccountA
UNION ALL
SELECT 'AccountB',             Id, Balance FROM AccountB;
GO
