namespace BeTendyBE.Domain
{
    public class User
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string FirstName { get; set; } = default!;
        public string LastName { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string? Phone { get; set; }
        public string Role { get; set; } = "client"; // client | master | admin
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public string PasswordHash { get; set; } = string.Empty;

        public ICollection<RefreshToken> RefreshTokens { get; } = new List<RefreshToken>();
    }
}
