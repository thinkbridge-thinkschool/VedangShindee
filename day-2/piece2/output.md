Building...
info: Microsoft.EntityFrameworkCore.Migrations[20411]
      Acquiring an exclusive lock for migration application. See https://aka.ms/efcore-docs-migrations-lock for more information if this takes too long.
info: Microsoft.EntityFrameworkCore.Database.Command[20101]
      Executed DbCommand (29ms) [Parameters=[], CommandType='Text', CommandTimeout='30']
      SELECT COUNT(*) FROM "sqlite_master" WHERE "name" = '__EFMigrationsLock' AND "type" = 'table';
info: Microsoft.EntityFrameworkCore.Database.Command[20101]
      Executed DbCommand (114ms) [Parameters=[], CommandType='Text', CommandTimeout='30']
      INSERT OR IGNORE INTO "__EFMigrationsLock"("Id", "Timestamp") VALUES(1, '2026-05-20 06:21:19.8055693+00:00');
      SELECT changes();
info: Microsoft.EntityFrameworkCore.Database.Command[20101]
      Executed DbCommand (1ms) [Parameters=[], CommandType='Text', CommandTimeout='30']
      CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
          "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
          "ProductVersion" TEXT NOT NULL
      );
info: Microsoft.EntityFrameworkCore.Database.Command[20101]
      Executed DbCommand (0ms) [Parameters=[], CommandType='Text', CommandTimeout='30']
      SELECT COUNT(*) FROM "sqlite_master" WHERE "name" = '__EFMigrationsHistory' AND "type" = 'table';
info: Microsoft.EntityFrameworkCore.Database.Command[20101]
      Executed DbCommand (1ms) [Parameters=[], CommandType='Text', CommandTimeout='30']
      SELECT "MigrationId", "ProductVersion"
      FROM "__EFMigrationsHistory"
      ORDER BY "MigrationId";
info: Microsoft.EntityFrameworkCore.Migrations[20405]
      No migrations were applied. The database is already up to date.
info: Microsoft.EntityFrameworkCore.Database.Command[20101]
      Executed DbCommand (66ms) [Parameters=[], CommandType='Text', CommandTimeout='30']
      DELETE FROM "__EFMigrationsLock";
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5051
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
info: Microsoft.Hosting.Lifetime[0]
      Hosting environment: Development
info: Microsoft.Hosting.Lifetime[0]
      Content root path: D:\Vedang\thinkschool\VedangShindee\day-2\piece2\QuotesApi

Passed  GetById_TokenCancelledMidRequest_AbortsWellBeforeRepoTimeout       
Passed  GetById_TokenCancelledMidRequest_Returns499OrThrowsTaskCancelled   
