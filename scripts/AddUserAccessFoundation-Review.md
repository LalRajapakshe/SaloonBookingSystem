# AddUserAccessFoundation — review before execution

Migration id: `20261015015246_AddUserAccessFoundation`

The script was produced from the EF Core migration `Up()` method. `dotnet ef migrations script` was run from `20261011150000_AddAppointmentServicePreferredTimes` to this migration, with a design-time connection that is not `saloonDB`. The script was not executed.

File: `d:\Projects\SaloonBooking\scripts\AddUserAccessFoundation.sql`

## What changes

- `Users.Username` becomes `nvarchar(100)`.
- `Users` gains `UserCategory` (default 1, General), `EmployeeId`, and `CustomerId`.
- Existing `Employees.UserId` values are copied onto `Users.EmployeeId`. Those users become category 2 (Employee), and their `Users.BranchId` is set from the employee row. The old column is dropped only after that copy is checked.
- Role and permission text columns are shortened: role code 20, role name 100, permission code 80, permission name 150, descriptions 300.
- Unique indexes: username per tenant among active rows, one user per employee, one user per customer, role code per tenant, permission code, and unique user-role and role-permission pairs.
- Check constraint `CK_Users_CategoryLinks` requires General users to have no links, Employee users to have only an employee, and Customer users to have only a customer.
- Replaced foreign keys on `UserRoles` and `RolePermissions` are `NO ACTION`. The previous keys were `ON DELETE CASCADE`.
- New foreign keys: `Users.EmployeeId` to `Employees`, `Users.CustomerId` to `Customers`. Neither cascades.
- Permission catalog, `ADMIN` / `STAFF` / `CUSTOMER` roles for each non-deleted tenant, and their role permissions are inserted when missing.
- Each non-deleted user with no role row is mapped to that tenant's active `ADMIN` role when `LOWER(Users.Role)` is `admin`, and to the active `STAFF` role otherwise.
- A non-deleted `ADMIN`, `STAFF`, or `CUSTOMER` role with `IsActive = 0` stops the migration (error 50029). The script does not reactivate it, rename it, or give it permissions or users. Deactivation and edits to a built-in role's name, description, and permissions are supported admin actions, so restoring those details would override intentional configuration.
- A deleted built-in role does not block the insert. A new active role with that code is created.
- Role permissions and user-role rows are inserted only for active built-in roles.
- After that, error 50035 stops the migration when a user on an active tenant has no active role. Error 50036 stops it when an active, non-customer user lacks `users.manage` (legacy admin) or `appointments.read` (everyone else) through an active role and an active permission. A `UserRoles` row on an inactive role is not treated as access.
- `__EFMigrationsHistory` receives `20261015015246_AddUserAccessFoundation` / `9.0.8` in the same transaction.

`Users.PasswordHash` and `Users.Role` are not updated. No user rows are inserted or deleted.

## Preconditions that stop the script

Errors 50001–50004 are the migration's own data guards: duplicate usernames in a tenant, username longer than 100, two employees sharing one user, or an employee link that crosses tenants.

Errors 50010–50028 stop before any change when history, the previous migration, required tables, `Employees.UserId`, `Users.Role`, or `Users.PasswordHash` are not in the expected state, when this migration's objects already exist, when a replaced key or index is missing, when role or permission text would not fit, or when duplicate role, permission, user-role, or role-permission rows would break the new unique indexes.

Errors 50031, 50032, and 50034 roll back if the employee-link copy count does not match, the user count changes, or `Employees.UserId` is still present. Errors 50029, 50035, and 50036 roll back when an inactive built-in role is present, a user has no active role, or an active user lacks the required effective permission.

## Expected result

- The history row exists and `Employees.UserId` does not.
- `Users.UserCategory` is 1 for users who had no employee link, including the existing Admin account. That account keeps its password hash, its `Users.Role` value, and its current branch, because branch is overwritten only when an employee link is copied.
- Users whose `Users.Role` is `admin` (any letter case) receive the `ADMIN` role. Other non-deleted users receive `STAFF`.
- Each non-deleted tenant has `ADMIN`, `STAFF`, and `CUSTOMER`. `ADMIN` receives every permission except `customer.*`. `STAFF` also excludes `users.*`, `roles.*`, `permissions.*`, and `tenants.manage`. `CUSTOMER` receives only `customer.*`.
- Appointment tables are not modified.

## Before you run it

1. Take a full backup of `saloonDB`.
2. Confirm the session database is `saloonDB`.
3. Run the script once. A second run stops because the history row is already present.
4. Deploy the API build that contains this migration before using the application. The new build reads `Users.UserCategory` and does not read `Employees.UserId`.

The script is one SQL batch. Statements that use the new `Users.CustomerId`, `Users.EmployeeId`, and `Users.UserCategory` columns run inside `EXEC`, because SQL Server compiles the batch before those columns exist. A direct reference in the same batch raises "invalid column name" before any change is applied.

Confirm with:

```sql
SELECT [MigrationId], [ProductVersion]
FROM [__EFMigrationsHistory]
WHERE [MigrationId] = N'20261015015246_AddUserAccessFoundation';

SELECT
    COL_LENGTH(N'Users', N'UserCategory') AS UserCategory,
    COL_LENGTH(N'Users', N'EmployeeId') AS EmployeeId,
    COL_LENGTH(N'Employees', N'UserId') AS EmployeesUserId;

SELECT [RoleCode], COUNT(*) AS RoleRows
FROM [Roles]
WHERE [IsDeleted] = 0
GROUP BY [RoleCode];

SELECT COUNT(*) AS ActivePermissions
FROM [Permissions]
WHERE [IsDeleted] = 0;
```

`ActivePermissions` should be 27. `RoleRows` should show `ADMIN`, `STAFF`, and `CUSTOMER`.

Recovery limits are in `AddUserAccessFoundation-Recovery.md`. A failed run rolls back. A successful run should be undone from the backup, not by dropping the new user, role, or permission data.
