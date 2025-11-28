using Swashbuckle.AspNetCore.Filters;

namespace BeTendlyBE.DTO;


public sealed class CreateServiceRequest
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int DurationMinutes { get; set; }
    public string? Description { get; set; }
}

public sealed class CreateServiceResponse
{
    public Guid Id { get; init; }
    public Guid MasterId { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public int DurationMinutes { get; init; }
    public string? Description { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class ServiceResponse200Example : IExamplesProvider<CreateServiceResponse>
{
    public CreateServiceResponse GetExamples() => new()
    {
        Id = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
        MasterId = Guid.Parse("11111111-2222-3333-4444-555555555555"),
        Name = "Стрижка жіноча",
        Price = 650,
        DurationMinutes = 60,
        Description = "Миття, стрижка, укладка",
        CreatedAtUtc = DateTime.UtcNow.AddDays(-10),
    };
}

