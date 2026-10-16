using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CabinBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProjectPurposeAndRoomNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CapacityLabel",
                table: "Cabins",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PurposeDetail",
                table: "Bookings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Cabins",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Capacity", "CapacityLabel", "Description", "Name" },
                values: new object[] { 9, "9+", "Large room for big groups", "Lunch Room" });

            migrationBuilder.UpdateData(
                table: "Cabins",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Capacity", "CapacityLabel", "Description", "Name" },
                values: new object[] { 5, "5+", "Meeting room with display", "Conference Room" });

            migrationBuilder.UpdateData(
                table: "Cabins",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Capacity", "CapacityLabel", "Description" },
                values: new object[] { 4, "3-4", "Small cabin for focused discussions" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CapacityLabel",
                table: "Cabins");

            migrationBuilder.DropColumn(
                name: "PurposeDetail",
                table: "Bookings");

            migrationBuilder.UpdateData(
                table: "Cabins",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Capacity", "Description", "Name" },
                values: new object[] { 4, "Small cabin for quick discussions", "Cabin 1" });

            migrationBuilder.UpdateData(
                table: "Cabins",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Capacity", "Description", "Name" },
                values: new object[] { 6, "Meeting cabin with display", "Cabin 2" });

            migrationBuilder.UpdateData(
                table: "Cabins",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Capacity", "Description" },
                values: new object[] { 8, "Conference cabin with VC setup" });
        }
    }
}
