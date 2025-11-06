using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeTendyBE.Migrations
{
    /// <inheritdoc />
    public partial class ChangeMasterKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 0) На всякий случай: убрать возможные NULL'ы user_id (если такие вдруг есть)
            migrationBuilder.Sql("""
        DELETE FROM master WHERE user_id IS NULL;
    """);

            // 1) Удалить дубликаты по user_id, оставив по одной записи (с наименьшим id)
            migrationBuilder.Sql("""
        WITH ranked AS (
            SELECT id, user_id,
                   ROW_NUMBER() OVER (PARTITION BY user_id ORDER BY id) AS rn
            FROM master
        )
        DELETE FROM master m
        USING ranked r
        WHERE m.id = r.id AND r.rn > 1;
    """);

            // 2) Если PK раньше был user_id — меняем на id
            migrationBuilder.DropPrimaryKey(
                name: "pk_master",
                table: "master");

            migrationBuilder.AddPrimaryKey(
                name: "pk_master",
                table: "master",
                column: "id");

            // 3) Теперь индекс станет уникальным без ошибок
            migrationBuilder.CreateIndex(
                name: "ix_master_user_id",
                table: "master",
                column: "user_id",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "pk_master",
                table: "master");

            migrationBuilder.DropIndex(
                name: "ix_master_user_id",
                table: "master");

            migrationBuilder.AddPrimaryKey(
                name: "pk_master",
                table: "master",
                column: "user_id");
        }

#nullable disable

    }
}