SET XACT_ABORT ON;
SET NOCOUNT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @ServerName sysname = CONVERT(sysname, @@SERVERNAME);
    DECLARE @DatabaseName sysname = DB_NAME();
    IF @ServerName <> N'NAGSERVER' OR @DatabaseName <> N'saloonDB'
        THROW 51000, 'Connected database is not the development salon database.', 1;

    DECLARE @UserRoleCountBefore int = (SELECT COUNT(*) FROM UserRoles);
    DECLARE @PlatformChecksumBefore int = (
        SELECT CHECKSUM_AGG(CHECKSUM(UserId, CONVERT(int, IsPlatformAdministrator)))
        FROM Users);
    DECLARE @TadminRoleCountBefore int = (
        SELECT COUNT(*)
        FROM Users u
        JOIN UserRoles ur ON ur.UserId = u.UserId
        WHERE u.Username = N'Tadmin' AND u.IsDeleted = 0);
    DECLARE @StaffAssignments int = (
        SELECT COUNT(*)
        FROM Roles r
        JOIN UserRoles ur ON ur.RoleId = r.RoleId
        WHERE r.RoleCode = N'STAFF' AND r.IsDeleted = 0);

    IF @StaffAssignments <> 0
        THROW 51001, 'STAFF still has user assignments. No role was retired.', 1;

    INSERT INTO Permissions (PermissionCode, PermissionName, Description, IsActive, CreatedDate, IsDeleted)
    SELECT v.PermissionCode, v.PermissionName, v.Description, 1, SYSUTCDATETIME(), 0
    FROM (VALUES
        (N'appointments.own.read', N'View own appointments', N'View appointments and service lines assigned to the signed-in employee.'),
        (N'appointments.own.progress', N'Progress own services', N'Commence and complete service lines assigned to the signed-in employee.'),
        (N'notifications.bookings.read', N'View new bookings', N'View new booking notifications.'),
        (N'notifications.cancellations.read', N'View cancellation requests', N'View customer cancellation requests.'),
        (N'notifications.confirmation.read', N'View confirmations', N'View appointments awaiting confirmation.')
    ) v(PermissionCode, PermissionName, Description)
    WHERE NOT EXISTS (
        SELECT 1 FROM Permissions p
        WHERE p.PermissionCode = v.PermissionCode AND p.IsDeleted = 0);

    INSERT INTO Roles (RoleCode, RoleName, Description, CreatedDate, TenantId, IsActive, IsDeleted)
    SELECT v.RoleCode, v.RoleName, v.Description, SYSUTCDATETIME(), t.TenantId, 1, 0
    FROM Tenants t
    CROSS JOIN (VALUES
        (N'STYLIST', N'Stylist', N'Works on assigned service lines.'),
        (N'RECEPTIONIST', N'Receptionist', N'Front desk.'),
        (N'SUPERVISOR', N'Supervisor', N'Operational oversight. Assigned only when a salon needs it.')
    ) v(RoleCode, RoleName, Description)
    WHERE t.IsDeleted = 0 AND t.IsActive = 1
      AND NOT EXISTS (
          SELECT 1 FROM Roles r
          WHERE r.TenantId = t.TenantId AND r.RoleCode = v.RoleCode AND r.IsDeleted = 0);

    DECLARE @Wanted TABLE (RoleCode nvarchar(20) NOT NULL, PermissionCode nvarchar(80) NOT NULL, PRIMARY KEY (RoleCode, PermissionCode));
    INSERT INTO @Wanted (RoleCode, PermissionCode) VALUES
        (N'ADMIN', N'appointments.manage'),
        (N'ADMIN', N'appointments.read'),
        (N'ADMIN', N'availability.read'),
        (N'ADMIN', N'branches.manage'),
        (N'ADMIN', N'branches.read'),
        (N'ADMIN', N'customers.manage'),
        (N'ADMIN', N'customers.read'),
        (N'ADMIN', N'employees.manage'),
        (N'ADMIN', N'employees.read'),
        (N'ADMIN', N'leaves.manage'),
        (N'ADMIN', N'leaves.read'),
        (N'ADMIN', N'permissions.read'),
        (N'ADMIN', N'qualifications.manage'),
        (N'ADMIN', N'qualifications.read'),
        (N'ADMIN', N'roles.manage'),
        (N'ADMIN', N'roles.read'),
        (N'ADMIN', N'schedules.manage'),
        (N'ADMIN', N'schedules.read'),
        (N'ADMIN', N'serviceCategories.manage'),
        (N'ADMIN', N'serviceCategories.read'),
        (N'ADMIN', N'services.manage'),
        (N'ADMIN', N'services.read'),
        (N'ADMIN', N'tenants.manage'),
        (N'ADMIN', N'tenants.read'),
        (N'ADMIN', N'users.manage'),
        (N'ADMIN', N'users.read'),
        (N'ADMIN', N'notifications.bookings.read'),
        (N'ADMIN', N'notifications.cancellations.read'),
        (N'ADMIN', N'notifications.confirmation.read'),
        (N'STYLIST', N'appointments.own.read'),
        (N'STYLIST', N'appointments.own.progress'),
        (N'RECEPTIONIST', N'appointments.read'),
        (N'RECEPTIONIST', N'appointments.manage'),
        (N'RECEPTIONIST', N'customers.read'),
        (N'RECEPTIONIST', N'customers.manage'),
        (N'RECEPTIONIST', N'services.read'),
        (N'RECEPTIONIST', N'branches.read'),
        (N'RECEPTIONIST', N'schedules.read'),
        (N'RECEPTIONIST', N'availability.read'),
        (N'RECEPTIONIST', N'notifications.bookings.read'),
        (N'RECEPTIONIST', N'notifications.cancellations.read'),
        (N'RECEPTIONIST', N'notifications.confirmation.read'),
        (N'SUPERVISOR', N'appointments.read'),
        (N'SUPERVISOR', N'schedules.read'),
        (N'CUSTOMER', N'customer.profile.read'),
        (N'CUSTOMER', N'customer.appointments.read'),
        (N'CUSTOMER', N'customer.appointments.create'),
        (N'CUSTOMER', N'customer.catalogue.read'),
        (N'CUSTOMER', N'customer.availability.read');

    INSERT INTO RolePermissions (RoleId, PermissionId)
    SELECT r.RoleId, p.PermissionId
    FROM Roles r
    JOIN Tenants t ON t.TenantId = r.TenantId AND t.IsDeleted = 0 AND t.IsActive = 1
    JOIN @Wanted w ON w.RoleCode = r.RoleCode
    JOIN Permissions p ON p.PermissionCode = w.PermissionCode AND p.IsDeleted = 0 AND p.IsActive = 1
    WHERE r.IsDeleted = 0
      AND NOT EXISTS (
          SELECT 1 FROM RolePermissions existing
          WHERE existing.RoleId = r.RoleId AND existing.PermissionId = p.PermissionId);

    UPDATE Roles
    SET IsActive = 0,
        ModifiedDate = SYSUTCDATETIME()
    WHERE RoleCode = N'STAFF'
      AND IsDeleted = 0
      AND IsActive = 1
      AND NOT EXISTS (SELECT 1 FROM UserRoles ur WHERE ur.RoleId = Roles.RoleId);

    IF (SELECT COUNT(*) FROM Permissions WHERE IsDeleted = 0) <> 37
        THROW 51002, 'Permission count is not 37.', 1;
    IF EXISTS (
        SELECT PermissionCode FROM Permissions WHERE IsDeleted = 0
        GROUP BY PermissionCode HAVING COUNT(*) > 1)
        THROW 51003, 'Duplicate permission codes.', 1;

    IF EXISTS (
        SELECT t.TenantId
        FROM Tenants t
        WHERE t.IsDeleted = 0 AND t.IsActive = 1
          AND (
              SELECT COUNT(*) FROM Roles r
              WHERE r.TenantId = t.TenantId AND r.IsDeleted = 0
                AND r.RoleCode IN (N'ADMIN', N'STAFF', N'CUSTOMER', N'STYLIST', N'RECEPTIONIST', N'SUPERVISOR')
          ) <> 6)
        THROW 51004, 'A tenant is missing a core role.', 1;

    IF EXISTS (
        SELECT r.RoleId
        FROM Roles r
        JOIN Tenants t ON t.TenantId = r.TenantId AND t.IsDeleted = 0 AND t.IsActive = 1
        JOIN @Wanted w ON w.RoleCode = r.RoleCode
        LEFT JOIN Permissions p ON p.PermissionCode = w.PermissionCode AND p.IsDeleted = 0
        LEFT JOIN RolePermissions rp ON rp.RoleId = r.RoleId AND rp.PermissionId = p.PermissionId
        WHERE r.IsDeleted = 0 AND rp.RolePermissionId IS NULL)
        THROW 51005, 'A required role permission is missing.', 1;

    IF EXISTS (
        SELECT r.RoleId
        FROM Roles r
        JOIN RolePermissions rp ON rp.RoleId = r.RoleId
        JOIN Permissions p ON p.PermissionId = rp.PermissionId
        WHERE r.IsDeleted = 0 AND r.RoleCode IN (N'STYLIST', N'RECEPTIONIST', N'SUPERVISOR', N'CUSTOMER')
          AND NOT EXISTS (
              SELECT 1 FROM @Wanted w
              WHERE w.RoleCode = r.RoleCode AND w.PermissionCode = p.PermissionCode))
        THROW 51006, 'A role has a permission outside its matrix.', 1;

    IF EXISTS (
        SELECT 1
        FROM Roles r
        JOIN RolePermissions rp ON rp.RoleId = r.RoleId
        JOIN Permissions p ON p.PermissionId = rp.PermissionId
        WHERE r.RoleCode = N'ADMIN' AND r.IsDeleted = 0
          AND p.PermissionCode IN (N'platform.manage', N'appointments.own.read', N'appointments.own.progress'))
        THROW 51007, 'ADMIN has a forbidden permission.', 1;

    IF EXISTS (
        SELECT 1
        FROM Users u
        JOIN UserRoles ur ON ur.UserId = u.UserId
        JOIN Roles r ON r.RoleId = ur.RoleId
        WHERE u.Username = N'Tadmin' AND u.IsDeleted = 0 AND r.RoleCode <> N'ADMIN')
        THROW 51008, 'Tadmin has a role other than ADMIN.', 1;

    IF (SELECT COUNT(*) FROM Users u JOIN UserRoles ur ON ur.UserId = u.UserId WHERE u.Username = N'Tadmin' AND u.IsDeleted = 0) <> @TadminRoleCountBefore
        THROW 51009, 'Tadmin role assignments changed.', 1;

    IF EXISTS (
        SELECT 1 FROM Users
        WHERE Username = N'Tadmin' AND (IsPlatformAdministrator = 1 OR IsDeleted = 1 OR IsActive = 0 OR UserCategory <> 1))
        THROW 51010, 'Tadmin is not an active salon administrator.', 1;

    IF (SELECT COUNT(*) FROM UserRoles) <> @UserRoleCountBefore
        THROW 51011, 'User role assignments changed.', 1;

    IF (SELECT CHECKSUM_AGG(CHECKSUM(UserId, CONVERT(int, IsPlatformAdministrator))) FROM Users) <> @PlatformChecksumBefore
        THROW 51012, 'A platform administrator flag changed.', 1;

    IF EXISTS (
        SELECT 1 FROM Roles
        WHERE RoleCode = N'STAFF' AND IsDeleted = 0 AND IsActive = 1)
        THROW 51013, 'An unassigned STAFF role is still active.', 1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
