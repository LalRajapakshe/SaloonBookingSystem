# Release soft-deleted employee and customer links — not applied

This change is not part of the running model and has not been applied to `saloonDB`.
Do not run `scripts/ReleaseSoftDeletedUserLinks.sql` until it is approved.
Do not copy `scripts/ReleaseSoftDeletedUserLinks.cs` into `SalonBooking.Persistence/Migrations`.
That folder is what `dotnet ef database update` applies.

## What the live indexes do

`IX_Users_EmployeeId` and `IX_Users_CustomerId` are unique for every non-null link.
Their filters are `[EmployeeId] IS NOT NULL` and `[CustomerId] IS NOT NULL`.
They do not exclude `IsDeleted = 1`.

A soft-deleted user therefore keeps the employee or customer link.
The username index is different: `IX_Users_Tenant_Username` applies only where `IsDeleted = 0`, so a deleted username can be reused.

The application now follows the link indexes. Creating another account for an employee or customer that is still referenced, including by a deleted user, is rejected as an employee or customer conflict. It is not reported as a duplicate username.

`UserConfiguration` still declares those indexes without an `IsDeleted` filter, so the EF model matches `saloonDB`.

## Why releasing the link is separate

Letting a new account reuse an employee or customer after the previous user is soft-deleted requires new index filters:

```text
[EmployeeId] IS NOT NULL AND [IsDeleted] = 0
[CustomerId] IS NOT NULL AND [IsDeleted] = 0
```

That is a schema change. It is not required to correct the error message, and it must not be applied to the live database from this change.

## If the release is approved later

1. Back up `saloonDB`.
2. Change `UserConfiguration` to the filters above and add a real EF migration with an updated model snapshot.
3. Review `scripts/ReleaseSoftDeletedUserLinks.sql`, set its apply flag only for that approved run, and run it once in the backed-up database.
4. Do not run the script and also let EF generate a second copy of the same index change.

The reference `Up` method in `scripts/ReleaseSoftDeletedUserLinks.cs` is not registered in the migrations assembly.
