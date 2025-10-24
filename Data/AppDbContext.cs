namespace BeTendyBE.Data
{
    using BeTendyBE.Domain;
    using Microsoft.EntityFrameworkCore;

    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<User> Users => Set<User>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            b.Entity<User>().HasIndex(x => x.Email).IsUnique();
            b.Entity<RefreshToken>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasIndex(x => x.TokenHash).IsUnique();

                e.HasOne(x => x.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
