using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeTendlyBE.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingServiceFK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
            name: "fk_booking_service_service_id",
            table: "booking",
            column: "service_id",
            principalTable: "service",
            principalColumn: "id",
            onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_booking_client_id_master_id_idempotency_key",
                table: "booking");

            migrationBuilder.DropIndex(
                name: "ix_booking_status_hold_expires_utc",
                table: "booking");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Bookings_StartOnHour",
                table: "booking");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:btree_gist", ",,");

            migrationBuilder.AlterColumn<DateTime>(
                name: "start_utc",
                table: "booking",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamptz");

            migrationBuilder.AlterColumn<DateTime>(
                name: "hold_expires_utc",
                table: "booking",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamptz",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "end_utc",
                table: "booking",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamptz");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at_utc",
                table: "booking",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamptz",
                oldDefaultValueSql: "NOW()");

            migrationBuilder.CreateIndex(
                name: "ix_booking_client_id",
                table: "booking",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "ix_booking_master_id_idempotency_key",
                table: "booking",
                columns: new[] { "master_id", "idempotency_key" },
                unique: true);
        }
    }
}
