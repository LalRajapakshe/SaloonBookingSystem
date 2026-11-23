using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalonBooking.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceLineStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LineStatus",
                table: "AppointmentServices",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.Sql("""
                UPDATE s
                SET LineStatus = CASE
                    WHEN s.IsCancelled = 1 THEN N'Cancelled'
                    WHEN a.Status = N'Completed' THEN N'Completed'
                    WHEN a.Status IN (N'Cancelled', N'NoShow') THEN N'Cancelled'
                    WHEN s.EmployeeId IS NOT NULL THEN N'Assigned'
                    ELSE N'Pending'
                END
                FROM AppointmentServices s
                INNER JOIN Appointments a ON a.AppointmentId = s.AppointmentId;

                UPDATE AppointmentServices
                SET IsCancelled = 1
                WHERE LineStatus = N'Cancelled' AND IsCancelled = 0;
                """);

            migrationBuilder.CreateTable(
                name: "AppointmentServiceStatusHistories",
                columns: table => new
                {
                    AppointmentServiceStatusHistoryId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    AppointmentId = table.Column<long>(type: "bigint", nullable: false),
                    AppointmentServiceId = table.Column<long>(type: "bigint", nullable: false),
                    OldStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    NewStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChangedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Action = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentServiceStatusHistories", x => x.AppointmentServiceStatusHistoryId);
                    table.ForeignKey(
                        name: "FK_AppointmentServiceStatusHistories_AppointmentServices_AppointmentServiceId",
                        column: x => x.AppointmentServiceId,
                        principalTable: "AppointmentServices",
                        principalColumn: "AppointmentServiceId");
                    table.ForeignKey(
                        name: "FK_AppointmentServiceStatusHistories_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "AppointmentId");
                    table.ForeignKey(
                        name: "FK_AppointmentServiceStatusHistories_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "BranchId");
                    table.ForeignKey(
                        name: "FK_AppointmentServiceStatusHistories_Users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_AppointmentService_LineStatus",
                table: "AppointmentServices",
                sql: "[LineStatus] IN ('Pending', 'Assigned', 'InProgress', 'Completed', 'Cancelled')");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentServiceStatusHistories_AppointmentId_ChangedAt",
                table: "AppointmentServiceStatusHistories",
                columns: new[] { "AppointmentId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentServiceStatusHistories_AppointmentServiceId_ChangedAt",
                table: "AppointmentServiceStatusHistories",
                columns: new[] { "AppointmentServiceId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentServiceStatusHistories_BranchId",
                table: "AppointmentServiceStatusHistories",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentServiceStatusHistories_ChangedByUserId",
                table: "AppointmentServiceStatusHistories",
                column: "ChangedByUserId");

            migrationBuilder.Sql("""
                INSERT INTO AppointmentServiceStatusHistories (
                    BranchId, AppointmentId, AppointmentServiceId, OldStatus, NewStatus, EmployeeId,
                    ChangedAt, ChangedByUserId, Reason, Action, CreatedDate, IsDeleted, IsActive, TenantId)
                SELECT
                    s.BranchId, s.AppointmentId, s.AppointmentServiceId, NULL, s.LineStatus, s.EmployeeId,
                    SYSUTCDATETIME(), NULL,
                    N'Initial status derived from the existing appointment and assignment.',
                    N'Backfill', SYSUTCDATETIME(), 0, 1, s.TenantId
                FROM AppointmentServices s
                WHERE NOT EXISTS (
                    SELECT 1 FROM AppointmentServiceStatusHistories h
                    WHERE h.AppointmentServiceId = s.AppointmentServiceId AND h.Action = N'Backfill');

                IF NOT EXISTS (
                    SELECT 1 FROM Permissions
                    WHERE PermissionCode = N'appointments.lines.progress' AND IsDeleted = 0)
                INSERT INTO Permissions (PermissionCode, PermissionName, Description, IsActive, CreatedDate, IsDeleted)
                VALUES (
                    N'appointments.lines.progress',
                    N'Progress branch services',
                    N'Commence and complete service lines assigned to other employees in an authorized branch.',
                    1, SYSUTCDATETIME(), 0);

                INSERT INTO RolePermissions (RoleId, PermissionId)
                SELECT r.RoleId, p.PermissionId
                FROM Roles r
                INNER JOIN Permissions p
                    ON p.PermissionCode = N'appointments.lines.progress' AND p.IsDeleted = 0 AND p.IsActive = 1
                WHERE r.RoleCode = N'SUPERVISOR' AND r.IsDeleted = 0 AND r.IsActive = 1
                  AND NOT EXISTS (
                    SELECT 1 FROM RolePermissions existing
                    WHERE existing.RoleId = r.RoleId AND existing.PermissionId = p.PermissionId);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE existing
                FROM RolePermissions existing
                INNER JOIN Permissions p ON p.PermissionId = existing.PermissionId
                WHERE p.PermissionCode = N'appointments.lines.progress';

                DELETE FROM Permissions
                WHERE PermissionCode = N'appointments.lines.progress';
                """);

            migrationBuilder.DropTable(
                name: "AppointmentServiceStatusHistories");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AppointmentService_LineStatus",
                table: "AppointmentServices");

            migrationBuilder.DropColumn(
                name: "LineStatus",
                table: "AppointmentServices");
        }
    }
}
