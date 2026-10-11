using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SalonBooking.Persistence.Context;

#nullable disable

namespace SalonBooking.Persistence.Migrations
{
    [DbContext(typeof(SalonBookingDbContext))]
    [Migration("20261011150000_AddAppointmentServicePreferredTimes")]
    public partial class AddAppointmentServicePreferredTimes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PreferredStartTime",
                table: "AppointmentServices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PreferredEndTime",
                table: "AppointmentServices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_AppointmentService_PreferredShape",
                table: "AppointmentServices",
                sql: "([PreferredStartTime] IS NULL AND [PreferredEndTime] IS NULL) OR ([PreferredStartTime] IS NOT NULL AND [PreferredEndTime] IS NOT NULL AND [PreferredEndTime] > [PreferredStartTime])");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AppointmentService_PreferredShape",
                table: "AppointmentServices");

            migrationBuilder.DropColumn(
                name: "PreferredEndTime",
                table: "AppointmentServices");

            migrationBuilder.DropColumn(
                name: "PreferredStartTime",
                table: "AppointmentServices");
        }
    }
}
