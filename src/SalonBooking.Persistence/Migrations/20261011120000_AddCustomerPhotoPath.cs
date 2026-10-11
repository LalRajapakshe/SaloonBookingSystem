using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SalonBooking.Persistence.Context;

#nullable disable

namespace SalonBooking.Persistence.Migrations
{
    [DbContext(typeof(SalonBookingDbContext))]
    [Migration("20261011120000_AddCustomerPhotoPath")]
    public partial class AddCustomerPhotoPath : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhotoPath",
                table: "Customers",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhotoPath",
                table: "Customers");
        }
    }
}
