namespace BeTendyBE.Domain
{
    public class User
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Email { get; set; } = default!;
        public string? Phone { get; set; }
        public string Role { get; set; } = "client"; // client | master | admin
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    }
}
