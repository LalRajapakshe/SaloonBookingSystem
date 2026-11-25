using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalonBooking.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Bills",
                columns: table => new
                {
                    BillId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    AppointmentId = table.Column<long>(type: "bigint", nullable: true),
                    BillNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PaymentStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BillDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FinalizedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VoidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VoidedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_Bills", x => x.BillId);
                    table.CheckConstraint("CK_Bill_Money", "CAST(Subtotal AS REAL) >= 0 AND CAST(TaxAmount AS REAL) >= 0 AND CAST(Total AS REAL) >= 0");
                    table.CheckConstraint("CK_Bill_PaymentStatus", "[PaymentStatus] IN ('Unpaid', 'PartiallyPaid', 'Paid')");
                    table.CheckConstraint("CK_Bill_Status", "[Status] IN ('Draft', 'Finalized', 'Void')");
                    table.ForeignKey(
                        name: "FK_Bills_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "AppointmentId");
                    table.ForeignKey(
                        name: "FK_Bills_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "BranchId");
                    table.ForeignKey(
                        name: "FK_Bills_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId");
                });

            migrationBuilder.CreateTable(
                name: "RetailProducts",
                columns: table => new
                {
                    RetailProductId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    ProductCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    SellingPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_RetailProducts", x => x.RetailProductId);
                    table.ForeignKey(
                        name: "FK_RetailProducts_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "BranchId");
                });

            migrationBuilder.CreateTable(
                name: "TaxSettings",
                columns: table => new
                {
                    TaxSettingId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    RatePercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_TaxSettings", x => x.TaxSettingId);
                    table.CheckConstraint("CK_TaxSetting_Rate", "CAST(RatePercent AS REAL) >= 0 AND CAST(RatePercent AS REAL) <= 100");
                    table.ForeignKey(
                        name: "FK_TaxSettings_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "BranchId");
                });

            migrationBuilder.CreateTable(
                name: "BillPayments",
                columns: table => new
                {
                    BillPaymentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    BillId = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Method = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecordedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_BillPayments", x => x.BillPaymentId);
                    table.CheckConstraint("CK_BillPayment_Method", "[Method] IN ('Cash', 'Card', 'BankTransfer', 'Other')");
                    table.CheckConstraint("CK_BillPayment_Shape", "(Kind = 'Payment' AND CAST(Amount AS REAL) > 0) OR (Kind = 'Refund' AND CAST(Amount AS REAL) < 0)");
                    table.ForeignKey(
                        name: "FK_BillPayments_Bills_BillId",
                        column: x => x.BillId,
                        principalTable: "Bills",
                        principalColumn: "BillId");
                });

            migrationBuilder.CreateTable(
                name: "BillLines",
                columns: table => new
                {
                    BillLineId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    BillId = table.Column<long>(type: "bigint", nullable: false),
                    LineType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxRatePercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AppointmentServiceId = table.Column<long>(type: "bigint", nullable: true),
                    RetailProductId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_BillLines", x => x.BillLineId);
                    table.CheckConstraint("CK_BillLine_Money", "Quantity > 0 AND CAST(UnitPrice AS REAL) >= 0 AND CAST(DiscountAmount AS REAL) = 0 AND CAST(TaxRatePercent AS REAL) >= 0 AND CAST(TaxRatePercent AS REAL) <= 100 AND CAST(TaxAmount AS REAL) >= 0 AND CAST(LineTotal AS REAL) >= 0");
                    table.CheckConstraint("CK_BillLine_Shape", "([LineType] = 'Service' AND [AppointmentServiceId] IS NOT NULL AND [RetailProductId] IS NULL) OR ([LineType] = 'Product' AND [RetailProductId] IS NOT NULL AND [AppointmentServiceId] IS NULL)");
                    table.CheckConstraint("CK_BillLine_Type", "[LineType] IN ('Service', 'Product')");
                    table.ForeignKey(
                        name: "FK_BillLines_AppointmentServices_AppointmentServiceId",
                        column: x => x.AppointmentServiceId,
                        principalTable: "AppointmentServices",
                        principalColumn: "AppointmentServiceId");
                    table.ForeignKey(
                        name: "FK_BillLines_Bills_BillId",
                        column: x => x.BillId,
                        principalTable: "Bills",
                        principalColumn: "BillId");
                    table.ForeignKey(
                        name: "FK_BillLines_RetailProducts_RetailProductId",
                        column: x => x.RetailProductId,
                        principalTable: "RetailProducts",
                        principalColumn: "RetailProductId");
                });

            migrationBuilder.CreateTable(
                name: "ActiveServiceCharges",
                columns: table => new
                {
                    AppointmentServiceId = table.Column<long>(type: "bigint", nullable: false),
                    BillId = table.Column<long>(type: "bigint", nullable: false),
                    BillLineId = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    BranchId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActiveServiceCharges", x => x.AppointmentServiceId);
                    table.ForeignKey(
                        name: "FK_ActiveServiceCharges_AppointmentServices_AppointmentServiceId",
                        column: x => x.AppointmentServiceId,
                        principalTable: "AppointmentServices",
                        principalColumn: "AppointmentServiceId");
                    table.ForeignKey(
                        name: "FK_ActiveServiceCharges_BillLines_BillLineId",
                        column: x => x.BillLineId,
                        principalTable: "BillLines",
                        principalColumn: "BillLineId");
                    table.ForeignKey(
                        name: "FK_ActiveServiceCharges_Bills_BillId",
                        column: x => x.BillId,
                        principalTable: "Bills",
                        principalColumn: "BillId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActiveServiceCharges_BillId",
                table: "ActiveServiceCharges",
                column: "BillId");

            migrationBuilder.CreateIndex(
                name: "IX_ActiveServiceCharges_BillLineId",
                table: "ActiveServiceCharges",
                column: "BillLineId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BillLines_AppointmentServiceId",
                table: "BillLines",
                column: "AppointmentServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_BillLines_BillId",
                table: "BillLines",
                column: "BillId");

            migrationBuilder.CreateIndex(
                name: "IX_BillLines_RetailProductId",
                table: "BillLines",
                column: "RetailProductId");

            migrationBuilder.CreateIndex(
                name: "IX_BillPayments_BillId",
                table: "BillPayments",
                column: "BillId");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_AppointmentId",
                table: "Bills",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_BranchId",
                table: "Bills",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_CustomerId",
                table: "Bills",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_TenantId_BillNumber",
                table: "Bills",
                columns: new[] { "TenantId", "BillNumber" },
                unique: true,
                filter: "[BillNumber] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_TenantId_BranchId_BillDate",
                table: "Bills",
                columns: new[] { "TenantId", "BranchId", "BillDate" });

            migrationBuilder.CreateIndex(
                name: "IX_RetailProducts_BranchId",
                table: "RetailProducts",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_RetailProducts_TenantId_BranchId_ProductCode",
                table: "RetailProducts",
                columns: new[] { "TenantId", "BranchId", "ProductCode" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TaxSettings_BranchId",
                table: "TaxSettings",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxSettings_TenantId_BranchId",
                table: "TaxSettings",
                columns: new[] { "TenantId", "BranchId" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActiveServiceCharges");

            migrationBuilder.DropTable(
                name: "BillPayments");

            migrationBuilder.DropTable(
                name: "TaxSettings");

            migrationBuilder.DropTable(
                name: "BillLines");

            migrationBuilder.DropTable(
                name: "Bills");

            migrationBuilder.DropTable(
                name: "RetailProducts");
        }
    }
}
