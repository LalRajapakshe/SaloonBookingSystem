/*
    Reference only. This file is not included in the API project.
    Do not move it into SalonBooking.Persistence/Migrations.
    dotnet ef database update would then apply it.

    The matching reviewed script is ReleaseSoftDeletedUserLinks.sql.
    Read ReleaseSoftDeletedUserLinks-Review.md before using either file.
*/

using Microsoft.EntityFrameworkCore.Migrations;

namespace SalonBooking.Scripts;

public partial class ReleaseSoftDeletedUserLinks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Users_EmployeeId",
            table: "Users");

        migrationBuilder.DropIndex(
            name: "IX_Users_CustomerId",
            table: "Users");

        migrationBuilder.CreateIndex(
            name: "IX_Users_EmployeeId",
            table: "Users",
            column: "EmployeeId",
            unique: true,
            filter: "[EmployeeId] IS NOT NULL AND [IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_Users_CustomerId",
            table: "Users",
            column: "CustomerId",
            unique: true,
            filter: "[CustomerId] IS NOT NULL AND [IsDeleted] = 0");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Users_EmployeeId",
            table: "Users");

        migrationBuilder.DropIndex(
            name: "IX_Users_CustomerId",
            table: "Users");

        migrationBuilder.CreateIndex(
            name: "IX_Users_EmployeeId",
            table: "Users",
            column: "EmployeeId",
            unique: true,
            filter: "[EmployeeId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_Users_CustomerId",
            table: "Users",
            column: "CustomerId",
            unique: true,
            filter: "[CustomerId] IS NOT NULL");
    }
}
