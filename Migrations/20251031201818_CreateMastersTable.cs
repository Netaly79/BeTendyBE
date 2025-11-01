using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeTendyBE.Migrations
{
    /// <inheritdoc />
    public partial class CreateMastersTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // видаляємо стару таблицю, якщо вона існує
            migrationBuilder.DropTable(
                name: "master_profile",
                schema: null);

            // створюємо нову таблицю "master"
            migrationBuilder.CreateTable(
                name: "master",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    about = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    skills = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    experience_years = table.Column<int>(type: "integer", nullable: true),
                    address = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_master", x => x.id);
                    table.ForeignKey(
                        name: "fk_master_user_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // додаємо унікальний індекс на user_id (1:1)
            migrationBuilder.CreateIndex(
                name: "ix_master_user_id",
                table: "master",
                column: "user_id",
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "master");

            migrationBuilder.CreateTable(
                name: "master_profile",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    about = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    skills = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    years_experience = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_master_profile", x => x.user_id);
                    table.ForeignKey(
                        name: "fk_master_profile_user_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }
    }
}
