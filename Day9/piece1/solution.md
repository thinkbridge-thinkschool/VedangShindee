# Isolation Levels & Read Anomalies

---

## Anomaly 1 — Dirty Read

A session reads **uncommitted** data written by another session that later rolls back.

### Session A (run first)
```sql
USE IsolationDemo;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

BEGIN TRANSACTION;
    UPDATE Accounts SET Balance = 99999.00 WHERE Id = 20; -- Tracy
    WAITFOR DELAY '00:00:30';
ROLLBACK;
```

### Session B (run while Session A is executing)
```sql
USE IsolationDemo;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT Id, Name, Balance FROM Accounts WHERE Id = 20;
-- Returns 99999.00 — dirty! Tracy never actually had this balance.
```

### Output
| Read | Balance |
|------|---------|
| Session B during Session A | 99999.00 (dirty — never committed) |
| After Session A rollback | 4000.00 (actual value) |

![Dirty Read Result](Dirty%20Read%20Result.png)

---

## Anomaly 2 — Non-Repeatable Read

The **same row** is read twice inside one transaction and the value changes between reads.

### Session A (run first)
```sql
USE IsolationDemo;
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

BEGIN TRANSACTION;
    SELECT Id, Name, Balance FROM Accounts WHERE Id = 12; -- Laura
    -- First result: 3000.00

    WAITFOR DELAY '00:00:30';

    SELECT Id, Name, Balance FROM Accounts WHERE Id = 12; -- Laura
    -- Second result: 9999.00 (changed!)
COMMIT;
```

### Session B (run while Session A is executing)
```sql
USE IsolationDemo;

BEGIN TRANSACTION;
    UPDATE Accounts SET Balance = 9999.00 WHERE Id = 12; -- Laura
COMMIT;
```

### Output
| Read | Balance |
|------|---------|
| First read (Session A) | 3000.00 |
| Second read (Session A) | 9999.00 — non-repeatable! |

![Non-Repeatable Read Result](Non-Repeatable%20Read%20Result.png)

---

## Anomaly 3 — Phantom Read

A range query returns **different rows** on two executions within one transaction.

### Session A (run first)
```sql
USE IsolationDemo;
SET TRANSACTION ISOLATION LEVEL REPEATABLE READ;

BEGIN TRANSACTION;
    SELECT * FROM Accounts WHERE Balance > 1500.00;
    -- First result: 5 rows (Hannah, Laura, Nancy, Pamela, Tracy)

    WAITFOR DELAY '00:00:30';

    SELECT * FROM Accounts WHERE Balance > 1500.00;
    -- Second result: 6 rows (Uma appears as phantom!)
COMMIT;
```

### Session B (run while Session A is executing)
```sql
USE IsolationDemo;

BEGIN TRANSACTION;
    INSERT INTO Accounts VALUES (21, 'Uma', 5000.00);
COMMIT;
```

### Output
| Read | Rows Returned |
|------|--------------|
| First read (Session A) | 5 rows — Hannah, Laura, Nancy, Pamela, Tracy |
| Second read (Session A) | 6 rows — Uma (phantom!) appeared mid-transaction |

![Phantom Read Result](Phantom%20Read%20Result.png)

---

## Summary Table: Anomaly → Lowest Isolation Level That Prevents It

| Anomaly | Lowest Level That Prevents It | How It Prevents It |
|---|---|---|
| Dirty Read | `READ COMMITTED` | Will not read uncommitted data from other transactions |
| Non-Repeatable Read | `REPEATABLE READ` | Holds shared locks on read rows for the entire transaction |
| Phantom Read | `SERIALIZABLE` | Holds range locks that block inserts matching the query range |

---

## Full Isolation Level Comparison

| Isolation Level | Dirty Read | Non-Repeatable Read | Phantom Read |
|---|---|---|---|
| READ UNCOMMITTED | Possible | Possible | Possible |
| READ COMMITTED | Prevented | Possible | Possible |
| REPEATABLE READ | Prevented | Prevented | Possible |
| SERIALIZABLE | Prevented | Prevented | Prevented |
