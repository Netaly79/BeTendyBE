namespace BeTendyBE.Domain
{
    public class Service
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid MasterId { get; set; }
        public Master Master { get; set; } = default!;

        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int DurationMinutes { get; set; }
        public string? Description { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAtUtc { get; set; }
    }
}