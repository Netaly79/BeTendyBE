using Swashbuckle.AspNetCore.Filters;

public sealed class ServiceListItemResponse
{
  public Guid Id { get; set; }
  public string Name { get; set; } = string.Empty;
  public decimal Price { get; set; }
  public int DurationMinutes { get; set; }
  public string? Description { get; set; }
  public DateTime CreatedAtUtc { get; set; }
  public DateTime? UpdatedAtUtc { get; set; }
}

public class ServiceListExample : IExamplesProvider<List<ServiceListItemResponse>>
{
    public List<ServiceListItemResponse> GetExamples() =>
        new()
        {
            new ServiceListItemResponse
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Brow shaping",
                Price = 25.00m,
                DurationMinutes = 45,
                Description = "Correction and shaping of eyebrows.",
                CreatedAtUtc = DateTime.UtcNow.AddMonths(-2),
                UpdatedAtUtc = DateTime.UtcNow.AddDays(-1)
            },
            new ServiceListItemResponse
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Name = "Lash lift",
                Price = 40.00m,
                DurationMinutes = 60,
                Description = "Lash lifting with keratin treatment.",
                CreatedAtUtc = DateTime.UtcNow.AddMonths(-1),
                UpdatedAtUtc = DateTime.UtcNow.AddDays(-3)
            }
        };
}