using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalonBooking.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAccessFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
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
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_RolePermissions_Permissions_PermissionId",
                table: "RolePermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_RolePermissions_Roles_RoleId",
                table: "RolePermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_UserRoles_Roles_RoleId",
                table: "UserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_UserRoles_Users_UserId",
                table: "UserRoles");

            migrationBuilder.DropIndex(
                name: "IX_UserRoles_UserId",
                table: "UserRoles");

            migrationBuilder.DropIndex(
                name: "IX_RolePermissions_RoleId",
                table: "RolePermissions");

            migrationBuilder.AlterColumn<string>(
                name: "Username",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<long>(
                name: "CustomerId",
                table: "Users",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "EmployeeId",
                table: "Users",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserCategory",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql(
                """
                UPDATE u
                SET u.[EmployeeId] = e.[EmployeeId],
                    u.[UserCategory] = 2,
                    u.[BranchId] = e.[BranchId]
                FROM [Users] AS u
                INNER JOIN [Employees] AS e ON e.[UserId] = u.[UserId]
                WHERE e.[UserId] IS NOT NULL;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Users_UserId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_UserId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Employees");

            migrationBuilder.AlterColumn<string>(
                name: "RoleName",
                table: "Roles",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "RoleCode",
                table: "Roles",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Roles",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PermissionName",
                table: "Permissions",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "PermissionCode",
                table: "Permissions",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Permissions",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_CustomerId",
                table: "Users",
                column: "CustomerId",
                unique: true,
                filter: "[CustomerId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_EmployeeId",
                table: "Users",
                column: "EmployeeId",
                unique: true,
                filter: "[EmployeeId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Tenant_Username",
                table: "Users",
                columns: new[] { "TenantId", "Username" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_CategoryLinks",
                table: "Users",
                sql: "([UserCategory] = 1 AND [EmployeeId] IS NULL AND [CustomerId] IS NULL) OR ([UserCategory] = 2 AND [EmployeeId] IS NOT NULL AND [CustomerId] IS NULL) OR ([UserCategory] = 3 AND [EmployeeId] IS NULL AND [CustomerId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_User_Role",
                table: "UserRoles",
                columns: new[] { "UserId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Tenant_RoleCode",
                table: "Roles",
                columns: new[] { "TenantId", "RoleCode" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_Role_Permission",
                table: "RolePermissions",
                columns: new[] { "RoleId", "PermissionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_PermissionCode",
                table: "Permissions",
                column: "PermissionCode",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_RolePermissions_Permissions_PermissionId",
                table: "RolePermissions",
                column: "PermissionId",
                principalTable: "Permissions",
                principalColumn: "PermissionId");

            migrationBuilder.AddForeignKey(
                name: "FK_RolePermissions_Roles_RoleId",
                table: "RolePermissions",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "RoleId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserRoles_Roles_RoleId",
                table: "UserRoles",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "RoleId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserRoles_Users_UserId",
                table: "UserRoles",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Customers_CustomerId",
                table: "Users",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "CustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Employees_EmployeeId",
                table: "Users",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "EmployeeId");

            migrationBuilder.Sql(
                """
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
                                WHEN LOWER(u.[Role]) = N'admin' THEN N'users.manage'
                                ELSE N'appointments.read'
                            END))
                    THROW 50036, 'An active user lacks the required effective permission. Admin accounts need users.manage and other accounts need appointments.read through an active role. The migration was rolled back.', 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RolePermissions_Permissions_PermissionId",
                table: "RolePermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_RolePermissions_Roles_RoleId",
                table: "RolePermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_UserRoles_Roles_RoleId",
                table: "UserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_UserRoles_Users_UserId",
                table: "UserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Customers_CustomerId",
                table: "Users");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Employees_EmployeeId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_CustomerId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_EmployeeId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_Tenant_Username",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_CategoryLinks",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_UserRoles_User_Role",
                table: "UserRoles");

            migrationBuilder.DropIndex(
                name: "IX_Roles_Tenant_RoleCode",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_RolePermissions_Role_Permission",
                table: "RolePermissions");

            migrationBuilder.DropIndex(
                name: "IX_Permissions_PermissionCode",
                table: "Permissions");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "UserCategory",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "Username",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "RoleName",
                table: "Roles",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "RoleCode",
                table: "Roles",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Roles",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PermissionName",
                table: "Permissions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "PermissionCode",
                table: "Permissions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(80)",
                oldMaxLength: 80);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Permissions",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AddColumn<long>(
                name: "UserId",
                table: "Employees",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_UserId",
                table: "UserRoles",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleId",
                table: "RolePermissions",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_UserId",
                table: "Employees",
                column: "UserId",
                unique: true,
                filter: "[UserId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Users_UserId",
                table: "Employees",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_RolePermissions_Permissions_PermissionId",
                table: "RolePermissions",
                column: "PermissionId",
                principalTable: "Permissions",
                principalColumn: "PermissionId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RolePermissions_Roles_RoleId",
                table: "RolePermissions",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "RoleId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserRoles_Roles_RoleId",
                table: "UserRoles",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "RoleId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserRoles_Users_UserId",
                table: "UserRoles",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
