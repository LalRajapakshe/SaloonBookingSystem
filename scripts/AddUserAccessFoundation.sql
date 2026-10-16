/*
    Migration: 20261015015246_AddUserAccessFoundation
    Database:  saloonDB (SQL Server)
    Source:    EF Core migration Up(), emitted by:

        dotnet ef migrations script
            20261011150000_AddAppointmentServicePreferredTimes
            20261015015246_AddUserAccessFoundation

    EF Core product version recorded in __EFMigrationsHistory: 9.0.8

    Run this script once, in saloonDB, after a full database backup.
    It does not contain passwords, password hashes, connection strings, or secrets.
    It does not read or write Users.PasswordHash.
    Employees.UserId is copied onto Users.EmployeeId before that column is dropped.

    Any error, including a failed precondition, rolls the transaction back
    and rethrows. Schema changes are not skipped when an object is missing.

    Statements that use Users.CustomerId, Users.EmployeeId, or Users.UserCategory
    run through EXEC. SQL Server compiles a batch before it runs, so those
    new columns cannot be named directly in this same batch.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @UserCount int;
    DECLARE @LinkedEmployees int;
    DECLARE @CopiedEmployees int;

    IF OBJECT_ID(N'[__EFMigrationsHistory]', N'U') IS NULL
        THROW 50010, 'The EF migrations history table is missing. This script expects the existing saloonDB schema.', 1;

    IF EXISTS (
        SELECT 1
        FROM [__EFMigrationsHistory]
        WHERE [MigrationId] = N'20261015015246_AddUserAccessFoundation')
        THROW 50011, 'Migration 20261015015246_AddUserAccessFoundation is already recorded. Do not apply it again.', 1;

    IF NOT EXISTS (
        SELECT 1
        FROM [__EFMigrationsHistory]
        WHERE [MigrationId] = N'20261011150000_AddAppointmentServicePreferredTimes')
        THROW 50012, 'Previous migration 20261011150000_AddAppointmentServicePreferredTimes is not recorded. Apply earlier migrations before this script.', 1;

    IF OBJECT_ID(N'[Users]', N'U') IS NULL
        OR OBJECT_ID(N'[Employees]', N'U') IS NULL
        OR OBJECT_ID(N'[Roles]', N'U') IS NULL
        OR OBJECT_ID(N'[Permissions]', N'U') IS NULL
        OR OBJECT_ID(N'[RolePermissions]', N'U') IS NULL
        OR OBJECT_ID(N'[UserRoles]', N'U') IS NULL
        OR OBJECT_ID(N'[Tenants]', N'U') IS NULL
        OR OBJECT_ID(N'[Customers]', N'U') IS NULL
        OR OBJECT_ID(N'[AppointmentServices]', N'U') IS NULL
        THROW 50013, 'A required table is missing. Expected Users, Employees, Roles, Permissions, RolePermissions, UserRoles, Tenants, Customers, and AppointmentServices.', 1;

    IF COL_LENGTH(N'AppointmentServices', N'PreferredStartTime') IS NULL
        OR COL_LENGTH(N'AppointmentServices', N'PreferredEndTime') IS NULL
        THROW 50014, 'AppointmentServices preferred-time columns are missing. The database is not at the expected pre-migration schema.', 1;

    IF COL_LENGTH(N'Employees', N'UserId') IS NULL
        THROW 50015, 'Employees.UserId is missing, so existing employee links cannot be preserved before that column is removed.', 1;

    IF COL_LENGTH(N'Users', N'Role') IS NULL
        THROW 50016, 'Users.Role is missing, so existing users cannot be mapped to ADMIN or STAFF.', 1;

    IF COL_LENGTH(N'Users', N'PasswordHash') IS NULL
        THROW 50017, 'Users.PasswordHash is missing. Stop. This script must not recreate user credentials.', 1;

    IF COL_LENGTH(N'Users', N'UserCategory') IS NOT NULL
        OR COL_LENGTH(N'Users', N'EmployeeId') IS NOT NULL
        OR COL_LENGTH(N'Users', N'CustomerId') IS NOT NULL
        THROW 50018, 'Users already has UserCategory, EmployeeId, or CustomerId, but this migration is not recorded. Stop and review the schema.', 1;

    IF OBJECT_ID(N'[CK_Users_CategoryLinks]', N'C') IS NOT NULL
        OR OBJECT_ID(N'[FK_Users_Customers_CustomerId]', N'F') IS NOT NULL
        OR OBJECT_ID(N'[FK_Users_Employees_EmployeeId]', N'F') IS NOT NULL
        THROW 50019, 'A target constraint for this migration already exists. Stop and review the schema.', 1;

    IF EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE [name] IN (
            N'IX_Users_CustomerId',
            N'IX_Users_EmployeeId',
            N'IX_Users_Tenant_Username',
            N'IX_UserRoles_User_Role',
            N'IX_Roles_Tenant_RoleCode',
            N'IX_RolePermissions_Role_Permission',
            N'IX_Permissions_PermissionCode')
          AND [object_id] IN (
            OBJECT_ID(N'[Users]'),
            OBJECT_ID(N'[UserRoles]'),
            OBJECT_ID(N'[Roles]'),
            OBJECT_ID(N'[RolePermissions]'),
            OBJECT_ID(N'[Permissions]')))
        THROW 50020, 'A target index for this migration already exists. Stop and review the schema.', 1;

    IF OBJECT_ID(N'[FK_RolePermissions_Permissions_PermissionId]', N'F') IS NULL
        OR OBJECT_ID(N'[FK_RolePermissions_Roles_RoleId]', N'F') IS NULL
        OR OBJECT_ID(N'[FK_UserRoles_Roles_RoleId]', N'F') IS NULL
        OR OBJECT_ID(N'[FK_UserRoles_Users_UserId]', N'F') IS NULL
        OR OBJECT_ID(N'[FK_Employees_Users_UserId]', N'F') IS NULL
        THROW 50021, 'A foreign key this migration replaces is missing. Stop and review the schema.', 1;

    IF NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE [name] = N'IX_UserRoles_UserId' AND [object_id] = OBJECT_ID(N'[UserRoles]'))
        OR NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE [name] = N'IX_RolePermissions_RoleId' AND [object_id] = OBJECT_ID(N'[RolePermissions]'))
        OR NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE [name] = N'IX_Employees_UserId' AND [object_id] = OBJECT_ID(N'[Employees]'))
        THROW 50022, 'An index this migration replaces is missing. Stop and review the schema.', 1;

    IF EXISTS (
        SELECT 1
        FROM [Roles]
        WHERE LEN([RoleCode]) > 20
           OR LEN([RoleName]) > 100
           OR LEN([Description]) > 300)
        THROW 50023, 'A role code, name, or description is longer than the new column size (20, 100, or 300).', 1;

    IF EXISTS (
        SELECT 1
        FROM [Permissions]
        WHERE LEN([PermissionCode]) > 80
           OR LEN([PermissionName]) > 150
           OR LEN([Description]) > 300)
        THROW 50024, 'A permission code, name, or description is longer than the new column size (80, 150, or 300).', 1;

    IF EXISTS (
        SELECT 1
        FROM [Roles]
        WHERE [IsDeleted] = 0
        GROUP BY [TenantId], [RoleCode]
        HAVING COUNT(*) > 1)
        THROW 50025, 'Duplicate role codes exist within a tenant. Resolve them before applying this migration.', 1;

    IF EXISTS (
        SELECT 1
        FROM [Permissions]
        WHERE [IsDeleted] = 0
        GROUP BY [PermissionCode]
        HAVING COUNT(*) > 1)
        THROW 50026, 'Duplicate permission codes exist. Resolve them before applying this migration.', 1;

    IF EXISTS (
        SELECT 1
        FROM [UserRoles]
        GROUP BY [UserId], [RoleId]
        HAVING COUNT(*) > 1)
        THROW 50027, 'Duplicate user-role rows exist. Resolve them before applying this migration.', 1;

    IF EXISTS (
        SELECT 1
        FROM [RolePermissions]
        GROUP BY [RoleId], [PermissionId]
        HAVING COUNT(*) > 1)
        THROW 50028, 'Duplicate role-permission rows exist. Resolve them before applying this migration.', 1;

    SELECT @UserCount = COUNT(*) FROM [Users];
    SELECT @LinkedEmployees = COUNT(*) FROM [Employees] WHERE [UserId] IS NOT NULL;

    /* Data guards from 20261015015246_AddUserAccessFoundation.Up */
    IF EXISTS (
        SELECT 1
        FROM [Users]
        WHERE [IsDeleted] = 0
        GROUP BY [TenantId], [Username]
        HAVING COUNT(*) > 1)
        THROW 50001, 'Duplicate usernames exist within a tenant. Resolve them before applying this migration.', 1;

    IF EXISTS (SELECT 1 FROM [Users] WHERE LEN([Username]) > 100)
        THROW 50002, 'A username is longer than 100 characters.', 1;

    IF EXISTS (
        SELECT 1
        FROM [Employees]
        WHERE [UserId] IS NOT NULL
        GROUP BY [UserId]
        HAVING COUNT(*) > 1)
        THROW 50003, 'More than one employee is linked to the same user.', 1;

    IF EXISTS (
        SELECT 1
        FROM [Employees] AS e
        INNER JOIN [Users] AS u ON u.[UserId] = e.[UserId]
        WHERE e.[UserId] IS NOT NULL AND e.[TenantId] <> u.[TenantId])
        THROW 50004, 'An employee user link crosses tenants.', 1;

    ALTER TABLE [RolePermissions] DROP CONSTRAINT [FK_RolePermissions_Permissions_PermissionId];

    ALTER TABLE [RolePermissions] DROP CONSTRAINT [FK_RolePermissions_Roles_RoleId];

    ALTER TABLE [UserRoles] DROP CONSTRAINT [FK_UserRoles_Roles_RoleId];

    ALTER TABLE [UserRoles] DROP CONSTRAINT [FK_UserRoles_Users_UserId];

    DROP INDEX [IX_UserRoles_UserId] ON [UserRoles];

    DROP INDEX [IX_RolePermissions_RoleId] ON [RolePermissions];

    DECLARE @var sysname;
    SELECT @var = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Users]') AND [c].[name] = N'Username');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [Users] DROP CONSTRAINT [' + @var + '];');
    ALTER TABLE [Users] ALTER COLUMN [Username] nvarchar(100) NOT NULL;

    ALTER TABLE [Users] ADD [CustomerId] bigint NULL;

    ALTER TABLE [Users] ADD [EmployeeId] bigint NULL;

    ALTER TABLE [Users] ADD [UserCategory] int NOT NULL DEFAULT 1;

    EXEC(N'
    UPDATE u
    SET u.[EmployeeId] = e.[EmployeeId],
        u.[UserCategory] = 2,
        u.[BranchId] = e.[BranchId]
    FROM [Users] AS u
    INNER JOIN [Employees] AS e ON e.[UserId] = u.[UserId]
    WHERE e.[UserId] IS NOT NULL;
    ');

    EXEC sys.sp_executesql
        N'SELECT @count = COUNT(*) FROM [Users] WHERE [UserCategory] = 2 AND [EmployeeId] IS NOT NULL;',
        N'@count int OUTPUT',
        @count = @CopiedEmployees OUTPUT;

    IF @CopiedEmployees <> @LinkedEmployees
        THROW 50031, 'Employee links were not all copied onto Users.EmployeeId. The migration was rolled back.', 1;

    ALTER TABLE [Employees] DROP CONSTRAINT [FK_Employees_Users_UserId];

    DROP INDEX [IX_Employees_UserId] ON [Employees];

    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Employees]') AND [c].[name] = N'UserId');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Employees] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [Employees] DROP COLUMN [UserId];

    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Roles]') AND [c].[name] = N'RoleName');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [Roles] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [Roles] ALTER COLUMN [RoleName] nvarchar(100) NOT NULL;

    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Roles]') AND [c].[name] = N'RoleCode');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [Roles] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [Roles] ALTER COLUMN [RoleCode] nvarchar(20) NOT NULL;

    DECLARE @var4 sysname;
    SELECT @var4 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Roles]') AND [c].[name] = N'Description');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [Roles] DROP CONSTRAINT [' + @var4 + '];');
    ALTER TABLE [Roles] ALTER COLUMN [Description] nvarchar(300) NULL;

    DECLARE @var5 sysname;
    SELECT @var5 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Permissions]') AND [c].[name] = N'PermissionName');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [Permissions] DROP CONSTRAINT [' + @var5 + '];');
    ALTER TABLE [Permissions] ALTER COLUMN [PermissionName] nvarchar(150) NOT NULL;

    DECLARE @var6 sysname;
    SELECT @var6 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Permissions]') AND [c].[name] = N'PermissionCode');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [Permissions] DROP CONSTRAINT [' + @var6 + '];');
    ALTER TABLE [Permissions] ALTER COLUMN [PermissionCode] nvarchar(80) NOT NULL;

    DECLARE @var7 sysname;
    SELECT @var7 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Permissions]') AND [c].[name] = N'Description');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [Permissions] DROP CONSTRAINT [' + @var7 + '];');
    ALTER TABLE [Permissions] ALTER COLUMN [Description] nvarchar(300) NULL;

    EXEC(N'CREATE UNIQUE INDEX [IX_Users_CustomerId] ON [Users] ([CustomerId]) WHERE [CustomerId] IS NOT NULL;');

    EXEC(N'CREATE UNIQUE INDEX [IX_Users_EmployeeId] ON [Users] ([EmployeeId]) WHERE [EmployeeId] IS NOT NULL;');

    CREATE UNIQUE INDEX [IX_Users_Tenant_Username] ON [Users] ([TenantId], [Username]) WHERE [IsDeleted] = 0;

    EXEC(N'ALTER TABLE [Users] ADD CONSTRAINT [CK_Users_CategoryLinks] CHECK (([UserCategory] = 1 AND [EmployeeId] IS NULL AND [CustomerId] IS NULL) OR ([UserCategory] = 2 AND [EmployeeId] IS NOT NULL AND [CustomerId] IS NULL) OR ([UserCategory] = 3 AND [EmployeeId] IS NULL AND [CustomerId] IS NOT NULL));');

    CREATE UNIQUE INDEX [IX_UserRoles_User_Role] ON [UserRoles] ([UserId], [RoleId]);

    CREATE UNIQUE INDEX [IX_Roles_Tenant_RoleCode] ON [Roles] ([TenantId], [RoleCode]) WHERE [IsDeleted] = 0;

    CREATE UNIQUE INDEX [IX_RolePermissions_Role_Permission] ON [RolePermissions] ([RoleId], [PermissionId]);

    CREATE UNIQUE INDEX [IX_Permissions_PermissionCode] ON [Permissions] ([PermissionCode]) WHERE [IsDeleted] = 0;

    ALTER TABLE [RolePermissions] ADD CONSTRAINT [FK_RolePermissions_Permissions_PermissionId] FOREIGN KEY ([PermissionId]) REFERENCES [Permissions] ([PermissionId]);

    ALTER TABLE [RolePermissions] ADD CONSTRAINT [FK_RolePermissions_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([RoleId]);

    ALTER TABLE [UserRoles] ADD CONSTRAINT [FK_UserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([RoleId]);

    ALTER TABLE [UserRoles] ADD CONSTRAINT [FK_UserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId]);

    EXEC(N'ALTER TABLE [Users] ADD CONSTRAINT [FK_Users_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([CustomerId]);');

    EXEC(N'ALTER TABLE [Users] ADD CONSTRAINT [FK_Users_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([EmployeeId]);');

    IF EXISTS (
        SELECT 1
        FROM [Roles]
        WHERE [IsDeleted] = 0
          AND [IsActive] = 0
          AND [RoleCode] IN (N'ADMIN', N'STAFF', N'CUSTOMER'))
        THROW 50029, 'A built-in role (ADMIN, STAFF, or CUSTOMER) is inactive. Reactivate it manually or mark it deleted before this migration. This migration will not reactivate it, restore its details, or assign users and permissions to it.', 1;

    INSERT INTO [Permissions] ([PermissionCode], [PermissionName], [Description], [IsActive], [IsDeleted], [CreatedDate])
    SELECT v.[PermissionCode], v.[PermissionName], v.[Description], 1, 0, SYSUTCDATETIME()
    FROM (VALUES
        ('users.read', 'View users', 'View user accounts in this salon.'),
        ('users.manage', 'Manage users', 'Create and update user accounts, including activation.'),
        ('roles.read', 'View roles', 'View roles and the permissions assigned to them.'),
        ('roles.manage', 'Manage roles', 'Create roles and change their permissions.'),
        ('permissions.read', 'View permissions', 'View the permission catalog.'),
        ('employees.read', 'View employees', 'View employee records.'),
        ('employees.manage', 'Manage employees', 'Create and update employee records and service skills.'),
        ('customers.read', 'View customers', 'View customer records.'),
        ('customers.manage', 'Manage customers', 'Create and update customer records and profile photos.'),
        ('services.read', 'View services', 'View services and prices.'),
        ('services.manage', 'Manage services', 'Create and update services.'),
        ('serviceCategories.read', 'View service categories', 'View service categories.'),
        ('serviceCategories.manage', 'Manage service categories', 'Create and update service categories.'),
        ('appointments.read', 'View appointments', 'View appointments and their services.'),
        ('appointments.manage', 'Manage appointments', 'Create, schedule, and update appointments.'),
        ('availability.read', 'View availability', 'View open appointment slots.'),
        ('schedules.read', 'View staff schedules', 'View staff working hours.'),
        ('schedules.manage', 'Manage staff schedules', 'Create and update staff working hours.'),
        ('leaves.read', 'View staff leave', 'View staff leave.'),
        ('leaves.manage', 'Manage staff leave', 'Create and update staff leave.'),
        ('branches.read', 'View branches', 'View branches.'),
        ('branches.manage', 'Manage branches', 'Create and update branches.'),
        ('tenants.read', 'View tenant', 'View the salon business profile.'),
        ('tenants.manage', 'Manage tenant', 'Update the salon business profile. Creating a new business stays closed.'),
        ('customer.profile.read', 'View own profile', 'View the signed-in customer''s own profile.'),
        ('customer.appointments.read', 'View own appointments', 'View the signed-in customer''s own appointments.'),
        ('customer.appointments.create', 'Create own appointments', 'Create an appointment for the signed-in customer.')
    ) AS v([PermissionCode], [PermissionName], [Description])
    WHERE NOT EXISTS (
        SELECT 1 FROM [Permissions] AS p
        WHERE p.[PermissionCode] = v.[PermissionCode] AND p.[IsDeleted] = 0);

    INSERT INTO [Roles] ([TenantId], [RoleCode], [RoleName], [Description], [IsActive], [IsDeleted], [CreatedDate])
    SELECT t.[TenantId], v.[RoleCode], v.[RoleName], v.[Description], 1, 0, SYSUTCDATETIME()
    FROM [Tenants] AS t
    CROSS JOIN (VALUES
        ('ADMIN', 'Admin', 'Tenant administrator'),
        ('STAFF', 'Staff', 'Salon staff'),
        ('CUSTOMER', 'Customer', 'Customer account')
    ) AS v([RoleCode], [RoleName], [Description])
    WHERE t.[IsDeleted] = 0
      AND NOT EXISTS (
        SELECT 1 FROM [Roles] AS r
        WHERE r.[TenantId] = t.[TenantId] AND r.[RoleCode] = v.[RoleCode] AND r.[IsDeleted] = 0);

    INSERT INTO [RolePermissions] ([RoleId], [PermissionId])
    SELECT r.[RoleId], p.[PermissionId]
    FROM [Roles] AS r
    INNER JOIN [Permissions] AS p ON p.[IsDeleted] = 0 AND p.[IsActive] = 1
    WHERE r.[IsDeleted] = 0
      AND r.[IsActive] = 1
      AND r.[RoleCode] IN (N'ADMIN', N'STAFF', N'CUSTOMER')
      AND (
        (r.[RoleCode] = 'ADMIN' AND p.[PermissionCode] NOT LIKE 'customer.%')
        OR (r.[RoleCode] = 'STAFF' AND p.[PermissionCode] NOT LIKE 'customer.%'
            AND p.[PermissionCode] NOT LIKE 'users.%'
            AND p.[PermissionCode] NOT LIKE 'roles.%'
            AND p.[PermissionCode] NOT LIKE 'permissions.%'
            AND p.[PermissionCode] <> 'tenants.manage')
        OR (r.[RoleCode] = 'CUSTOMER' AND p.[PermissionCode] LIKE 'customer.%'))
      AND NOT EXISTS (
        SELECT 1 FROM [RolePermissions] AS rp
        WHERE rp.[RoleId] = r.[RoleId] AND rp.[PermissionId] = p.[PermissionId]);

    INSERT INTO [UserRoles] ([UserId], [RoleId])
    SELECT u.[UserId], r.[RoleId]
    FROM [Users] AS u
    INNER JOIN [Roles] AS r
        ON r.[TenantId] = u.[TenantId]
       AND r.[IsDeleted] = 0
       AND r.[IsActive] = 1
       AND r.[RoleCode] = CASE WHEN LOWER(u.[Role]) = 'admin' THEN 'ADMIN' ELSE 'STAFF' END
    WHERE u.[IsDeleted] = 0
      AND NOT EXISTS (SELECT 1 FROM [UserRoles] AS ur WHERE ur.[UserId] = u.[UserId]);

    IF EXISTS (
        SELECT 1
        FROM [Users] AS u
        INNER JOIN [Tenants] AS t ON t.[TenantId] = u.[TenantId]
        WHERE u.[IsDeleted] = 0
          AND t.[IsDeleted] = 0
          AND NOT EXISTS (
              SELECT 1
              FROM [UserRoles] AS ur
              INNER JOIN [Roles] AS r ON r.[RoleId] = ur.[RoleId]
              WHERE ur.[UserId] = u.[UserId]
                AND r.[TenantId] = u.[TenantId]
                AND r.[IsDeleted] = 0
                AND r.[IsActive] = 1))
        THROW 50035, 'A user has no active role. An assignment to only an inactive role does not grant access. The migration was rolled back.', 1;

    EXEC(N'
    IF EXISTS (
        SELECT 1
        FROM [Users] AS u
        INNER JOIN [Tenants] AS t ON t.[TenantId] = u.[TenantId]
        WHERE u.[IsDeleted] = 0
          AND u.[IsActive] = 1
          AND u.[UserCategory] <> 3
          AND t.[IsDeleted] = 0
          AND NOT EXISTS (
              SELECT 1
              FROM [UserRoles] AS ur
              INNER JOIN [Roles] AS r ON r.[RoleId] = ur.[RoleId]
              INNER JOIN [RolePermissions] AS rp ON rp.[RoleId] = r.[RoleId]
              INNER JOIN [Permissions] AS p ON p.[PermissionId] = rp.[PermissionId]
              WHERE ur.[UserId] = u.[UserId]
                AND r.[TenantId] = u.[TenantId]
                AND r.[IsActive] = 1
                AND r.[IsDeleted] = 0
                AND p.[IsActive] = 1
                AND p.[IsDeleted] = 0
                AND p.[PermissionCode] = CASE
                    WHEN LOWER(u.[Role]) = N''admin'' THEN N''users.manage''
                    ELSE N''appointments.read''
                END))
        THROW 50036, ''An active user lacks the required effective permission. Admin accounts need users.manage and other accounts need appointments.read through an active role. The migration was rolled back.'', 1;
    ');

    IF (SELECT COUNT(*) FROM [Users]) <> @UserCount
        THROW 50032, 'The user count changed. This migration must not insert or delete users.', 1;

    IF COL_LENGTH(N'Employees', N'UserId') IS NOT NULL
        THROW 50034, 'Employees.UserId is still present after the migration steps. The migration was rolled back.', 1;

    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261015015246_AddUserAccessFoundation', N'9.0.8');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
