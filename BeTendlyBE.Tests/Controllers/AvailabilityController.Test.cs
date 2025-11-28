using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

using BeTendlyBE.Controllers;
using BeTendlyBE.Contracts;
using BeTendlyBE.Data;
using BeTendlyBE.Domain;

namespace BeTendlyBE.Tests.Controllers;

public class AvailabilityControllerTests
{
    [Fact]
    public async Task GetFreeSlotsForDay_ValidRequest_ReturnsOkWithSlots()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"availability_happy_{Guid.NewGuid()}")
            .Options;

        await using var db = new AppDbContext(options);

        var masterId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();

        db.Services.Add(new Service
        {
            Id = serviceId,
            MasterId = masterId,
            DurationMinutes = 60
        });

        await db.SaveChangesAsync();

        var controller = new AvailabilityController();

        var date = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1));

        // Act
        var result = await controller.GetFreeSlotsForDay(
            db,
            masterId,
            serviceId,
            date,
            CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var slots = Assert.IsAssignableFrom<IEnumerable<SlotResponse>>(okResult.Value);
        var list = slots.ToList();

        Assert.NotEmpty(list);              
        Assert.True(list.Zip(list.Skip(1), (a, b) => a.StartUtc <= b.StartUtc).All(x => x));
    }
}
