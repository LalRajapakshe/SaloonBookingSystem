using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalonBooking.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentSchedulingFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "UserId",
                table: "Employees",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Appointments",
                columns: table => new
                {
                    AppointmentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    AppointmentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_Appointments", x => x.AppointmentId);
                    table.ForeignKey(
                        name: "FK_Appointments_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "BranchId");
                    table.ForeignKey(
                        name: "FK_Appointments_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId");
                    table.ForeignKey(
                        name: "FK_Appointments_Users_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "EmployeeServices",
                columns: table => new
                {
                    EmployeeServiceId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    ServiceId = table.Column<long>(type: "bigint", nullable: false),
                    PreferenceOrder = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_EmployeeServices", x => x.EmployeeServiceId);
                    table.ForeignKey(
                        name: "FK_EmployeeServices_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "BranchId");
                    table.ForeignKey(
                        name: "FK_EmployeeServices_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId");
                    table.ForeignKey(
                        name: "FK_EmployeeServices_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "ServiceId");
                });

            migrationBuilder.CreateTable(
                name: "StaffLeaves",
                columns: table => new
                {
                    StaffLeaveId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    StartDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LeaveType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
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
                    table.PrimaryKey("PK_StaffLeaves", x => x.StaffLeaveId);
                    table.CheckConstraint("CK_StaffLeave_EndAfterStart", "[EndDateTime] > [StartDateTime]");
                    table.ForeignKey(
                        name: "FK_StaffLeaves_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "BranchId");
                    table.ForeignKey(
                        name: "FK_StaffLeaves_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId");
                });

            migrationBuilder.CreateTable(
                name: "StaffSchedules",
                columns: table => new
                {
                    StaffScheduleId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    DayOfWeek = table.Column<byte>(type: "tinyint", nullable: false),
                    SegmentType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
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
                    table.PrimaryKey("PK_StaffSchedules", x => x.StaffScheduleId);
                    table.CheckConstraint("CK_StaffSchedule_DayOfWeek", "[DayOfWeek] >= 0 AND [DayOfWeek] <= 6");
                    table.CheckConstraint("CK_StaffSchedule_EndAfterStart", "[EndTime] > [StartTime]");
                    table.ForeignKey(
                        name: "FK_StaffSchedules_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "BranchId");
                    table.ForeignKey(
                        name: "FK_StaffSchedules_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId");
                });

            migrationBuilder.CreateTable(
                name: "AppointmentServices",
                columns: table => new
                {
                    AppointmentServiceId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    AppointmentId = table.Column<long>(type: "bigint", nullable: false),
                    ServiceId = table.Column<long>(type: "bigint", nullable: false),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: true),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsCancelled = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
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
                    table.PrimaryKey("PK_AppointmentServices", x => x.AppointmentServiceId);
                    table.CheckConstraint("CK_AppointmentService_ScheduleShape", "([StartTime] IS NULL AND [EndTime] IS NULL) OR ([EmployeeId] IS NOT NULL AND [StartTime] IS NOT NULL AND [EndTime] IS NOT NULL AND [EndTime] > [StartTime])");
                    table.ForeignKey(
                        name: "FK_AppointmentServices_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "AppointmentId");
                    table.ForeignKey(
                        name: "FK_AppointmentServices_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "BranchId");
                    table.ForeignKey(
                        name: "FK_AppointmentServices_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId");
                    table.ForeignKey(
                        name: "FK_AppointmentServices_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "ServiceId");
                });

            migrationBuilder.CreateTable(
                name: "AppointmentStatusHistories",
                columns: table => new
                {
                    AppointmentStatusHistoryId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    AppointmentId = table.Column<long>(type: "bigint", nullable: false),
                    OldStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    NewStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChangedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_AppointmentStatusHistories", x => x.AppointmentStatusHistoryId);
                    table.ForeignKey(
                        name: "FK_AppointmentStatusHistories_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "AppointmentId");
                    table.ForeignKey(
                        name: "FK_AppointmentStatusHistories_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "BranchId");
                    table.ForeignKey(
                        name: "FK_AppointmentStatusHistories_Users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "AppointmentServiceScheduleHistories",
                columns: table => new
                {
                    AppointmentServiceScheduleHistoryId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    AppointmentServiceId = table.Column<long>(type: "bigint", nullable: false),
                    OldEmployeeId = table.Column<long>(type: "bigint", nullable: true),
                    NewEmployeeId = table.Column<long>(type: "bigint", nullable: true),
                    OldStartTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NewStartTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OldEndTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NewEndTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChangedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_AppointmentServiceScheduleHistories", x => x.AppointmentServiceScheduleHistoryId);
                    table.ForeignKey(
                        name: "FK_AppointmentServiceScheduleHistories_AppointmentServices_AppointmentServiceId",
                        column: x => x.AppointmentServiceId,
                        principalTable: "AppointmentServices",
                        principalColumn: "AppointmentServiceId");
                    table.ForeignKey(
                        name: "FK_AppointmentServiceScheduleHistories_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "BranchId");
                    table.ForeignKey(
                        name: "FK_AppointmentServiceScheduleHistories_Employees_NewEmployeeId",
                        column: x => x.NewEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId");
                    table.ForeignKey(
                        name: "FK_AppointmentServiceScheduleHistories_Employees_OldEmployeeId",
                        column: x => x.OldEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId");
                    table.ForeignKey(
                        name: "FK_AppointmentServiceScheduleHistories_Users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Employees_UserId",
                table: "Employees",
                column: "UserId",
                unique: true,
                filter: "[UserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_BranchId_AppointmentDate_Status",
                table: "Appointments",
                columns: new[] { "BranchId", "AppointmentDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_CancelledByUserId",
                table: "Appointments",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_CustomerId",
                table: "Appointments",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentServices_AppointmentId_Sequence",
                table: "AppointmentServices",
                columns: new[] { "AppointmentId", "Sequence" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentServices_BranchId",
                table: "AppointmentServices",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentServices_EmployeeId_StartTime_EndTime",
                table: "AppointmentServices",
                columns: new[] { "EmployeeId", "StartTime", "EndTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentServices_ServiceId",
                table: "AppointmentServices",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentServiceScheduleHistories_AppointmentServiceId_ChangedAt",
                table: "AppointmentServiceScheduleHistories",
                columns: new[] { "AppointmentServiceId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentServiceScheduleHistories_BranchId",
                table: "AppointmentServiceScheduleHistories",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentServiceScheduleHistories_ChangedByUserId",
                table: "AppointmentServiceScheduleHistories",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentServiceScheduleHistories_NewEmployeeId",
                table: "AppointmentServiceScheduleHistories",
                column: "NewEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentServiceScheduleHistories_OldEmployeeId",
                table: "AppointmentServiceScheduleHistories",
                column: "OldEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentStatusHistories_AppointmentId_ChangedAt",
                table: "AppointmentStatusHistories",
                columns: new[] { "AppointmentId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentStatusHistories_BranchId",
                table: "AppointmentStatusHistories",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentStatusHistories_ChangedByUserId",
                table: "AppointmentStatusHistories",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeServices_BranchId_ServiceId_IsActive",
                table: "EmployeeServices",
                columns: new[] { "BranchId", "ServiceId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeServices_EmployeeId_ServiceId",
                table: "EmployeeServices",
                columns: new[] { "EmployeeId", "ServiceId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeServices_ServiceId",
                table: "EmployeeServices",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffLeaves_BranchId",
                table: "StaffLeaves",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffLeaves_EmployeeId_StartDateTime_EndDateTime_Status",
                table: "StaffLeaves",
                columns: new[] { "EmployeeId", "StartDateTime", "EndDateTime", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffSchedules_BranchId",
                table: "StaffSchedules",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffSchedules_EmployeeId_DayOfWeek_SegmentType",
                table: "StaffSchedules",
                columns: new[] { "EmployeeId", "DayOfWeek", "SegmentType" });

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Users_UserId",
                table: "Employees",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Users_UserId",
                table: "Employees");

            migrationBuilder.DropTable(
                name: "AppointmentServiceScheduleHistories");

            migrationBuilder.DropTable(
                name: "AppointmentStatusHistories");

            migrationBuilder.DropTable(
                name: "EmployeeServices");

            migrationBuilder.DropTable(
                name: "StaffLeaves");

            migrationBuilder.DropTable(
                name: "StaffSchedules");

            migrationBuilder.DropTable(
                name: "AppointmentServices");

            migrationBuilder.DropTable(
                name: "Appointments");

            migrationBuilder.DropIndex(
                name: "IX_Employees_UserId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Employees");
        }
    }
}
