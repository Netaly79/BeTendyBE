namespace BeTendyBE.Domain
{
    public enum UserRole { Client = 0, Master = 1, Admin = 2 }

    public class User
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        public string? Phone { get; set; }
        public string? AvatarUrl { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public string PasswordHash { get; set; } = string.Empty;

        public UserRole Role { get; set; } = UserRole.Client;

        public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

        public Master? Master { get; set; }
    }

    public class Master
    {
        public Guid Id { get; set; }                   
        public Guid UserId { get; set; }               

        public string? About { get; set; }
        public string? Skills { get; set; }
        public int? ExperienceYears { get; set; }
        public string? Address { get; set; }

        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }

        public User User { get; set; } = default!;
    }
}
