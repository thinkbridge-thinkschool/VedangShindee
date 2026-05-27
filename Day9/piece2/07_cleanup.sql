-- ============================================================
-- 07_cleanup.sql  –  Tear down everything after the exercise
-- ============================================================

-- Stop and drop the XE session
ALTER EVENT SESSION CaptureDeadlocks ON SERVER STATE = STOP;
DROP  EVENT SESSION CaptureDeadlocks ON SERVER;
GO

-- Turn off trace flag
DBCC TRACEOFF(1222, -1);
GO

-- Drop demo database
USE master;
DROP DATABASE IF EXISTS DeadlockDemo;
GO

PRINT 'Cleanup complete.';
GO
