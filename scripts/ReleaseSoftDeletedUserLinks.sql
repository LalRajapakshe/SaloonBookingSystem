/*
    Optional later change: let a new user account reuse an employee or customer
    after the previous user row is soft-deleted.

    This script is not an EF migration and has not been executed.
    The flag below is 0, so running this file rolls back and throws.
    Do not change the flag or run it against saloonDB until the review
    in ReleaseSoftDeletedUserLinks-Review.md is approved.

    It does not read or write passwords, hashes, or account names.
    It rebuilds two unique indexes. Existing rows are preserved.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @ApplyReleaseSoftDeletedUserLinks bit = 0;

    IF @ApplyReleaseSoftDeletedUserLinks <> 1
        THROW 50050, 'ReleaseSoftDeletedUserLinks is not approved. The script was rolled back without changing indexes.', 1;

    IF DB_NAME() <> N'saloonDB'
        THROW 50051, 'This script is written for saloonDB.', 1;

    IF NOT EXISTS (
        SELECT 1
        FROM [__EFMigrationsHistory]
        WHERE [MigrationId] = N'20261015015246_AddUserAccessFoundation')
        THROW 50052, 'AddUserAccessFoundation is not recorded in this database.', 1;

    IF EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE [name] = N'IX_Users_EmployeeId'
          AND [filter_definition] LIKE N'%IsDeleted%')
        THROW 50053, 'IX_Users_EmployeeId already excludes deleted rows.', 1;

    IF EXISTS (
        SELECT 1
        FROM [Users]
        WHERE [IsDeleted] = 0
          AND [EmployeeId] IS NOT NULL
        GROUP BY [EmployeeId]
        HAVING COUNT(*) > 1)
        THROW 50054, 'Two active users already share an employee. Resolve that before changing the index.', 1;

    IF EXISTS (
        SELECT 1
        FROM [Users]
        WHERE [IsDeleted] = 0
          AND [CustomerId] IS NOT NULL
        GROUP BY [CustomerId]
        HAVING COUNT(*) > 1)
        THROW 50055, 'Two active users already share a customer. Resolve that before changing the index.', 1;

    DROP INDEX [IX_Users_EmployeeId] ON [Users];
    CREATE UNIQUE INDEX [IX_Users_EmployeeId]
        ON [Users] ([EmployeeId])
        WHERE [EmployeeId] IS NOT NULL AND [IsDeleted] = 0;

    DROP INDEX [IX_Users_CustomerId] ON [Users];
    CREATE UNIQUE INDEX [IX_Users_CustomerId]
        ON [Users] ([CustomerId])
        WHERE [CustomerId] IS NOT NULL AND [IsDeleted] = 0;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH
