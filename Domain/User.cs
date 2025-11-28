using System.Text.Json.Serialization;

namespace BeTendlyBE.Domain
{

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

        public bool IsMaster { get; set; } = false;
 
        public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

        public Master? Master { get; set; }
    }

    public class Master
    {
        public Guid Id { get; set; }                   
        public Guid UserId { get; set; }               

        public string? About { get; set; }
        public List<string> Skills { get; set; } = new();
        public int? ExperienceYears { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        public ICollection<Service> Services { get; set; } = new List<Service>();

        [JsonIgnore]
        public User User { get; set; } = default!;
    }
}
