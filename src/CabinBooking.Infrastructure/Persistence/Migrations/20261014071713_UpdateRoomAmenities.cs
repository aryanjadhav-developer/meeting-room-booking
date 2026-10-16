using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CabinBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateRoomAmenities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Cabins",
                keyColumn: "Id",
                keyValue: 1,
                column: "Amenities",
                value: "TV,Whiteboard");

            migrationBuilder.UpdateData(
                table: "Cabins",
                keyColumn: "Id",
                keyValue: 3,
                column: "Amenities",
                value: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Cabins",
                keyColumn: "Id",
                keyValue: 1,
                column: "Amenities",
                value: "Whiteboard");

            migrationBuilder.UpdateData(
                table: "Cabins",
                keyColumn: "Id",
                keyValue: 3,
                column: "Amenities",
                value: "TV,Whiteboard,VC");
        }
    }
}
