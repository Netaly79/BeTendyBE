using Microsoft.EntityFrameworkCore.Migrations;


#nullable disable


namespace BeTendlyBE.Migrations
{
public partial class AddMasterIndexes : Migration
{
protected override void Up(MigrationBuilder migrationBuilder)
{
// 1) Try to enable pg_trgm only if allow‑listed; don't fail migration if not allowed (Azure)
migrationBuilder.Sql(@"
DO $$
BEGIN
IF EXISTS (SELECT 1 FROM pg_available_extensions WHERE name = 'pg_trgm') THEN
BEGIN
CREATE EXTENSION IF NOT EXISTS pg_trgm;
EXCEPTION WHEN OTHERS THEN
RAISE NOTICE 'pg_trgm not allowed, skipping';
END;
END IF;
END $$;", suppressTransaction: true);


// 2) Index for skills (GIN over text[])
migrationBuilder.Sql(@"CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_master_skills_gin
ON master USING gin (skills);", suppressTransaction: true);


// 3) Sort/filter helpers
migrationBuilder.Sql(@"CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_master_updated_at
ON master (updated_at_utc DESC);", suppressTransaction: true);


migrationBuilder.Sql(@"CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_master_experience
ON master (experience_years);", suppressTransaction: true);


// 4) Trigram indexes only if extension actually installed
migrationBuilder.Sql(@"
DO $$
BEGIN
IF EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'pg_trgm') THEN
EXECUTE 'CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_master_address_trgm ON master USING gin (address gin_trgm_ops)';
EXECUTE 'CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_master_about_trgm ON master USING gin (about gin_trgm_ops)';
ELSE
RAISE NOTICE 'pg_trgm not installed; skipping trigram indexes on address/about';
END IF;
END $$;", suppressTransaction: true);
}


protected override void Down(MigrationBuilder migrationBuilder)
{
// Drop created indexes (safe even if they do not exist)
migrationBuilder.Sql(@"DROP INDEX IF EXISTS idx_master_about_trgm;", suppressTransaction: true);
migrationBuilder.Sql(@"DROP INDEX IF EXISTS idx_master_address_trgm;", suppressTransaction: true);
migrationBuilder.Sql(@"DROP INDEX IF EXISTS idx_master_experience;", suppressTransaction: true);
migrationBuilder.Sql(@"DROP INDEX IF EXISTS idx_master_updated_at;", suppressTransaction: true);
migrationBuilder.Sql(@"DROP INDEX IF EXISTS idx_master_skills_gin;", suppressTransaction: true);
// Do not drop extension automatically.
}
}
}