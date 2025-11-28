using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeTendlyBE.Migrations
{
    public partial class AddBooking : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No btree_gist on Azure — leave extension annotations out.

            migrationBuilder.CreateTable(
                name: "booking",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    master_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false), // make nullable if you need guest bookings
                    service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    start_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    hold_expires_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_booking", x => x.id);
                    table.CheckConstraint("CK_Bookings_StartBeforeEnd", "end_utc > start_utc");

                    table.ForeignKey(
                        name: "fk_booking_master_master_id",
                        column: x => x.master_id,
                        principalTable: "master",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);

                    table.ForeignKey(
                        name: "fk_booking_user_client_id",
                        column: x => x.client_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade // or SetNull if client_id becomes nullable
                    );
                });

            // Basic indexes
            migrationBuilder.CreateIndex(
                name: "ix_booking_master_id_start_utc_end_utc",
                table: "booking",
                columns: new[] { "master_id", "start_utc", "end_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_booking_master_id_idempotency_key",
                table: "booking",
                columns: new[] { "master_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_booking_client_id",
                table: "booking",
                column: "client_id");

            // For cleaner finding expired holds quickly
            migrationBuilder.CreateIndex(
                name: "ix_booking_status_hold_expires",
                table: "booking",
                columns: new[] { "status", "hold_expires_utc" });

            migrationBuilder.Sql(@"
DROP INDEX IF EXISTS ix_booking_active_master_period;

CREATE INDEX IF NOT EXISTS ix_booking_confirmed_master_period
ON ""booking"" (""master_id"", ""start_utc"", ""end_utc"")
WHERE (""status"" = 1);

CREATE INDEX IF NOT EXISTS ix_booking_pending_master_period
ON ""booking"" (""master_id"", ""start_utc"", ""end_utc"")
WHERE (""status"" = 0);
");

            // Trigger to prevent overlaps (replaces EXCLUDE)
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION booking_prevent_overlap() RETURNS trigger AS $$
BEGIN
  IF NEW.status IN (0,1) THEN  -- Pending(0) or Confirmed(1)
    PERFORM 1 FROM booking b
     WHERE b.master_id = NEW.master_id
       AND b.status IN (0,1)
       AND (b.hold_expires_utc IS NULL OR b.hold_expires_utc > NOW())
       AND NOT (NEW.end_utc <= b.start_utc OR NEW.start_utc >= b.end_utc)
       AND b.id <> NEW.id;
    IF FOUND THEN
      RAISE EXCEPTION 'Booking overlaps an existing one for this master';
    END IF;
  END IF;
  RETURN NEW;
END; $$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_booking_prevent_overlap ON ""booking"";
CREATE TRIGGER trg_booking_prevent_overlap
BEFORE INSERT OR UPDATE ON ""booking""
FOR EACH ROW EXECUTE FUNCTION booking_prevent_overlap();
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS trg_booking_prevent_overlap ON ""booking"";");
            migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS booking_prevent_overlap();");

            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_booking_confirmed_master_period;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_booking_pending_master_period;");

            migrationBuilder.DropIndex(
                name: "ix_booking_status_hold_expires",
                table: "booking");

            migrationBuilder.DropTable(name: "booking");
        }
    }
}
