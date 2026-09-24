/* =====================================================================
   FaqCms — step 1 of 2: create the database only.

   Run this file by itself first (F5 / Execute — not "Parse", not
   "Display Estimated Execution Plan"). Once it finishes, close this
   query window / open a new one, connect to the FaqCms database, and
   run 02_create_schema.sql.

   Keeping this in its own file means no tool can ever statically check
   table/column names against a database that doesn't exist yet — that
   static check (not a real execution error) is what produces walls of
   "Invalid object name" / "Invalid column name" messages if you try to
   validate the whole thing in one pass before the tables exist.
   ===================================================================== */

IF DB_ID(N'FaqCms') IS NULL
BEGIN
    CREATE DATABASE FaqCms;
END
GO

PRINT 'Database FaqCms is ready. Now connect to it and run 02_create_schema.sql.';
GO
