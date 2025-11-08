using Microsoft.EntityFrameworkCore;
using BeTendyBE.Domain;

namespace BeTendyBE.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Master> Masters => Set<Master>();

    public DbSet<Service> Services => Set<Service>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // ----- User -----
        b.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);

            e.Property(x => x.Email).IsRequired().HasMaxLength(256);
            e.HasIndex(x => x.Email).IsUnique();

            e.Property(x => x.PasswordHash).IsRequired();

            e.Property(x => x.FirstName).HasMaxLength(100);
            e.Property(x => x.LastName).HasMaxLength(100);
            e.Property(x => x.Phone).HasMaxLength(32);
            e.Property(x => x.AvatarUrl).HasMaxLength(512);

            e.Property(x => x.IsMaster)
             .IsRequired();

            e.Property(x => x.CreatedAtUtc).IsRequired();

            e.HasIndex(x => x.IsMaster);
            e.HasIndex(x => x.CreatedAtUtc);

            e.HasMany(x => x.RefreshTokens)
             .WithOne(rt => rt.User)
             .HasForeignKey(rt => rt.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ----- RefreshToken -----
        b.Entity<RefreshToken>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TokenHash).IsUnique();

            e.Property(x => x.TokenHash).IsRequired();
            e.Property(x => x.ExpiresAtUtc).IsRequired();

            e.HasOne(x => x.User)
             .WithMany(u => u.RefreshTokens)
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ----- Master (1–1 к User, PK = FK) -----
        b.Entity<Master>(e =>
        {
            e.HasKey(x => x.Id);                       // PK = Id
            e.HasIndex(x => x.UserId).IsUnique();      // 1:1 гарантия
            e.Property(x => x.About).HasMaxLength(300);
            e.Property(x => x.Address).HasMaxLength(300);
            e.Property(x => x.Skills)
                .HasColumnType("text[]")
                .HasDefaultValueSql("'{}'::text[]") // пустой массив
                .IsRequired();
            e.Property(x => x.CreatedAtUtc).HasColumnType("timestamptz").IsRequired();
            e.Property(x => x.UpdatedAtUtc).HasColumnType("timestamptz").IsRequired();

            e.HasOne(x => x.User)
            .WithOne(u => u.Master)
            .HasForeignKey<Master>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Services -----
        b.Entity<Service>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Name).IsRequired().HasMaxLength(100);
            e.Property(s => s.Price).HasColumnType("decimal(10,2)").IsRequired();
            e.Property(s => s.DurationMinutes).IsRequired();
            e.Property(s => s.Description).HasColumnType("text");

            e.HasOne(s => s.Master)
            .WithMany(m => m.Services)
            .HasForeignKey(s => s.MasterId)
            .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
