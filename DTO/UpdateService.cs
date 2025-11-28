using Swashbuckle.AspNetCore.Filters;

namespace BeTendlyBE.DTO;

public sealed class UpdateServiceRequest
{
  public string Name { get; set; } = default!;
  public decimal Price { get; set; }
  public int DurationMinutes { get; set; }
  public string? Description { get; set; }
}

public sealed class UpdateServiceRequestExample : IExamplesProvider<UpdateServiceRequest>
{
  public UpdateServiceRequest GetExamples() => new()
  {
    Name = "Haircut + Wash",
    Price = 24.99m,
    DurationMinutes = 45,
    Description = "Classic haircut with wash and quick styling."
  };
}

public sealed class UpdateService200Example : IExamplesProvider<CreateServiceResponse>
{
  public CreateServiceResponse GetExamples() => new()
  {
    Id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
    MasterId = Guid.Parse("11111111-2222-3333-4444-555555555555"),
    Name = "Haircut + Wash",
    Price = 24.99m,
    DurationMinutes = 45,
    Description = "Classic haircut with wash and quick styling.",
    CreatedAtUtc = DateTime.UtcNow.AddDays(-10),
  };
}