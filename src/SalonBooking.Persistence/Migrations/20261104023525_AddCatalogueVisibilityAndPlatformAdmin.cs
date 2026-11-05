using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalonBooking.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogueVisibilityAndPlatformAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPlatformAdministrator",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsCustomerVisible",
                table: "Services",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPopular",
                table: "Services",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PhotoPath",
                table: "Services",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCustomerVisible",
                table: "Employees",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PhotoPath",
                table: "Employees",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfessionalDescription",
                table: "Employees",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Specialisation",
                table: "Employees",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Highlight",
                table: "Branches",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCustomerVisible",
                table: "Branches",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PhotoPath",
                table: "Branches",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PreferredEmployeeId",
                table: "AppointmentServices",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Qualifications",
                columns: table => new
                {
                    QualificationId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QualificationName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    QualificationLevel = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_Qualifications", x => x.QualificationId);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeQualifications",
                columns: table => new
                {
                    EmployeeQualificationId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    QualificationId = table.Column<long>(type: "bigint", nullable: false),
                    AwardingInstitution = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CompletionYear = table.Column<int>(type: "int", nullable: true),
                    CertificatePath = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    VerificationStatus = table.Column<int>(type: "int", nullable: false),
                    IsCustomerVisible = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_EmployeeQualifications", x => x.EmployeeQualificationId);
                    table.ForeignKey(
                        name: "FK_EmployeeQualifications_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId");
                    table.ForeignKey(
                        name: "FK_EmployeeQualifications_Qualifications_QualificationId",
                        column: x => x.QualificationId,
                        principalTable: "Qualifications",
                        principalColumn: "QualificationId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentServices_PreferredEmployeeId",
                table: "AppointmentServices",
                column: "PreferredEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeQualifications_EmployeeId_QualificationId",
                table: "EmployeeQualifications",
                columns: new[] { "EmployeeId", "QualificationId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeQualifications_QualificationId",
                table: "EmployeeQualifications",
                column: "QualificationId");

            migrationBuilder.CreateIndex(
                name: "IX_Qualifications_TenantId_QualificationName",
                table: "Qualifications",
                columns: new[] { "TenantId", "QualificationName" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeQualifications");

            migrationBuilder.DropTable(
                name: "Qualifications");

            migrationBuilder.DropIndex(
                name: "IX_AppointmentServices_PreferredEmployeeId",
                table: "AppointmentServices");

            migrationBuilder.DropColumn(
                name: "IsPlatformAdministrator",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsCustomerVisible",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "IsPopular",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "PhotoPath",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "IsCustomerVisible",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "PhotoPath",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "ProfessionalDescription",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "Specialisation",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "Highlight",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "IsCustomerVisible",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "PhotoPath",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "PreferredEmployeeId",
                table: "AppointmentServices");
        }
    }
}
