using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalonBooking.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRentalAndFeedbackFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_BillLine_Shape",
                table: "BillLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BillLine_Type",
                table: "BillLines");

            migrationBuilder.AddColumn<long>(
                name: "RentalReservationId",
                table: "BillLines",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RentalAssets",
                columns: table => new
                {
                    RentalAssetId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    AssetCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AssetName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ChargePerUse = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CleaningBufferMinutes = table.Column<int>(type: "int", nullable: false),
                    IsUnderMaintenance = table.Column<bool>(type: "bit", nullable: false),
                    ConditionNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PhotoPath = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
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
                    table.PrimaryKey("PK_RentalAssets", x => x.RentalAssetId);
                    table.CheckConstraint("CK_RentalAsset_Money", "CAST(ChargePerUse AS REAL) >= 0 AND CleaningBufferMinutes >= 0 AND CleaningBufferMinutes <= 10080");
                    table.ForeignKey(
                        name: "FK_RentalAssets_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "BranchId");
                });

            migrationBuilder.CreateTable(
                name: "ServiceFeedback",
                columns: table => new
                {
                    ServiceFeedbackId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    AppointmentId = table.Column<long>(type: "bigint", nullable: false),
                    AppointmentServiceId = table.Column<long>(type: "bigint", nullable: false),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    Rating = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_ServiceFeedback", x => x.ServiceFeedbackId);
                    table.CheckConstraint("CK_ServiceFeedback_Rating", "[Rating] >= 1 AND [Rating] <= 5");
                    table.ForeignKey(
                        name: "FK_ServiceFeedback_AppointmentServices_AppointmentServiceId",
                        column: x => x.AppointmentServiceId,
                        principalTable: "AppointmentServices",
                        principalColumn: "AppointmentServiceId");
                    table.ForeignKey(
                        name: "FK_ServiceFeedback_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId");
                });

            migrationBuilder.CreateTable(
                name: "RentalReservations",
                columns: table => new
                {
                    RentalReservationId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    RentalAssetId = table.Column<long>(type: "bigint", nullable: false),
                    AppointmentId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PlannedIssueAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PlannedReturnAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActualIssuedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualReturnedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IssuedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ReturnedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    IssueCondition = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReturnCondition = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ConditionDiscrepancy = table.Column<bool>(type: "bit", nullable: false),
                    ChargePerUse = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_RentalReservations", x => x.RentalReservationId);
                    table.CheckConstraint("CK_RentalReservation_Status", "[Status] IN ('Reserved', 'Issued', 'Returned', 'Cancelled')");
                    table.CheckConstraint("CK_RentalReservation_Window", "[PlannedReturnAt] > [PlannedIssueAt] AND CAST(ChargePerUse AS REAL) >= 0");
                    table.ForeignKey(
                        name: "FK_RentalReservations_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "AppointmentId");
                    table.ForeignKey(
                        name: "FK_RentalReservations_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId");
                    table.ForeignKey(
                        name: "FK_RentalReservations_RentalAssets_RentalAssetId",
                        column: x => x.RentalAssetId,
                        principalTable: "RentalAssets",
                        principalColumn: "RentalAssetId");
                });

            migrationBuilder.CreateTable(
                name: "ActiveRentalCharges",
                columns: table => new
                {
                    RentalReservationId = table.Column<long>(type: "bigint", nullable: false),
                    BillId = table.Column<long>(type: "bigint", nullable: false),
                    BillLineId = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    BranchId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActiveRentalCharges", x => x.RentalReservationId);
                    table.ForeignKey(
                        name: "FK_ActiveRentalCharges_BillLines_BillLineId",
                        column: x => x.BillLineId,
                        principalTable: "BillLines",
                        principalColumn: "BillLineId");
                    table.ForeignKey(
                        name: "FK_ActiveRentalCharges_Bills_BillId",
                        column: x => x.BillId,
                        principalTable: "Bills",
                        principalColumn: "BillId");
                    table.ForeignKey(
                        name: "FK_ActiveRentalCharges_RentalReservations_RentalReservationId",
                        column: x => x.RentalReservationId,
                        principalTable: "RentalReservations",
                        principalColumn: "RentalReservationId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_BillLines_RentalReservationId",
                table: "BillLines",
                column: "RentalReservationId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BillLine_Shape",
                table: "BillLines",
                sql: "([LineType] = 'Service' AND [AppointmentServiceId] IS NOT NULL AND [RetailProductId] IS NULL AND [RentalReservationId] IS NULL) OR ([LineType] = 'Product' AND [RetailProductId] IS NOT NULL AND [AppointmentServiceId] IS NULL AND [RentalReservationId] IS NULL) OR ([LineType] = 'Rental' AND [RentalReservationId] IS NOT NULL AND [AppointmentServiceId] IS NULL AND [RetailProductId] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BillLine_Type",
                table: "BillLines",
                sql: "[LineType] IN ('Service', 'Product', 'Rental')");

            migrationBuilder.CreateIndex(
                name: "IX_ActiveRentalCharges_BillId",
                table: "ActiveRentalCharges",
                column: "BillId");

            migrationBuilder.CreateIndex(
                name: "IX_ActiveRentalCharges_BillLineId",
                table: "ActiveRentalCharges",
                column: "BillLineId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RentalAssets_BranchId",
                table: "RentalAssets",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_RentalAssets_TenantId_BranchId_AssetCode",
                table: "RentalAssets",
                columns: new[] { "TenantId", "BranchId", "AssetCode" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RentalReservations_AppointmentId",
                table: "RentalReservations",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_RentalReservations_CustomerId",
                table: "RentalReservations",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_RentalReservations_RentalAssetId_PlannedIssueAt",
                table: "RentalReservations",
                columns: new[] { "RentalAssetId", "PlannedIssueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceFeedback_AppointmentServiceId",
                table: "ServiceFeedback",
                column: "AppointmentServiceId",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceFeedback_EmployeeId",
                table: "ServiceFeedback",
                column: "EmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_BillLines_RentalReservations_RentalReservationId",
                table: "BillLines",
                column: "RentalReservationId",
                principalTable: "RentalReservations",
                principalColumn: "RentalReservationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BillLines_RentalReservations_RentalReservationId",
                table: "BillLines");

            migrationBuilder.DropTable(
                name: "ActiveRentalCharges");

            migrationBuilder.DropTable(
                name: "ServiceFeedback");

            migrationBuilder.DropTable(
                name: "RentalReservations");

            migrationBuilder.DropTable(
                name: "RentalAssets");

            migrationBuilder.DropIndex(
                name: "IX_BillLines_RentalReservationId",
                table: "BillLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BillLine_Shape",
                table: "BillLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BillLine_Type",
                table: "BillLines");

            migrationBuilder.DropColumn(
                name: "RentalReservationId",
                table: "BillLines");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BillLine_Shape",
                table: "BillLines",
                sql: "([LineType] = 'Service' AND [AppointmentServiceId] IS NOT NULL AND [RetailProductId] IS NULL) OR ([LineType] = 'Product' AND [RetailProductId] IS NOT NULL AND [AppointmentServiceId] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BillLine_Type",
                table: "BillLines",
                sql: "[LineType] IN ('Service', 'Product')");
        }
    }
}
