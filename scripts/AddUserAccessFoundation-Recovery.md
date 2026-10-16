# AddUserAccessFoundation — recovery

This is guidance only. It is not a rollback script, and it is not safe to treat schema reversal as a way to recover production data.

## If the script fails

The script runs in one transaction with `XACT_ABORT ON`. The `CATCH` block rolls back when a transaction is still open, then rethrows the original error.

After a failure, confirm the migration row is absent and `Employees.UserId` is still present:

```sql
SELECT [MigrationId]
FROM [__EFMigrationsHistory]
WHERE [MigrationId] = N'20261015015246_AddUserAccessFoundation';

SELECT COL_LENGTH(N'Employees', N'UserId') AS EmployeesUserId;
```

The first query should return no row. `EmployeesUserId` should not be null. If both are true, the database is unchanged and the script can be corrected and run again. If the history row exists, the script committed; do not run it again.

## If error 50029, 50035, or 50036 is raised

The transaction is rolled back. Resolve the role data, then run the script again.

- 50029: a non-deleted `ADMIN`, `STAFF`, or `CUSTOMER` role is inactive. Reactivate that role yourself, or mark that row deleted if it should be replaced. The script will not do either.
- 50035: a user on an active tenant has no active role. An inactive role assignment does not count.
- 50036: an active user does not effectively have `users.manage` (when `Users.Role` is `admin`) or `appointments.read` (otherwise).

## If the script succeeds

The reliable recovery is the full `saloonDB` backup taken immediately before execution. Restore that backup.

Do not use the EF Core `Down()` method on the live database. It was reviewed and it is not a safe rollback:

- It drops `Users.UserCategory`, `Users.EmployeeId`, and `Users.CustomerId`, so category and employee or customer links are lost.
- It adds `Employees.UserId` back as an empty column. It does not copy `Users.EmployeeId` back into it.
- It does not delete the seeded permissions, roles, role permissions, or user-role rows.
- It puts `ON DELETE CASCADE` back on `UserRoles` and `RolePermissions`.
- Any user, role, or permission change made after the migration would be discarded or left inconsistent by a later schema reversal.

No destructive rollback script is included for that reason.
