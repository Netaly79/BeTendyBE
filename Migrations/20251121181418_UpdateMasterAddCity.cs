using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeTendyBE.Migrations
{
    /// <inheritdoc />
    public partial class UpdateMasterAddCity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "city",
                table: "master",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_booking_service_service_id",
                table: "booking");

        }
    }
}
