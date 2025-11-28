using Microsoft.EntityFrameworkCore;
using Xunit;

using BeTendlyBE.Data;
using BeTendlyBE.Domain;
using BeTendlyBE.Services;

namespace BeTendlyBE.Tests.Services;

public class BookingServiceTests
{
  private static AppDbContext CreateDbContext()
  {
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"booking_service_{Guid.NewGuid()}")
        .Options;

    return new AppDbContext(options);
  }

  [Fact]
  public async Task CreateAsync_ValidRequest_CreatesBookingAndReturnsResponse()
  {
    // Arrange
    await using var db = CreateDbContext();

    var masterUserId = Guid.NewGuid();
    var masterId = Guid.NewGuid();
    var clientId = Guid.NewGuid();
    var serviceId = Guid.NewGuid();

    var masterUser = new User
    {
      Id = masterUserId,
      FirstName = "John",
      LastName = "Master"
    };

    var client = new User
    {
      Id = clientId,
      FirstName = "Alice",
      LastName = "Client"
    };

    var master = new Master
    {
      Id = masterId,
      UserId = masterUserId,
      User = masterUser
    };

    var service = new Service
    {
      Id = serviceId,
      MasterId = masterId,
      DurationMinutes = 60,
      Name = "Haircut"
    };

    db.Users.Add(masterUser);
    db.Users.Add(client);
    db.Masters.Add(master);
    db.Services.Add(service);
    await db.SaveChangesAsync();

    var svc = new BookingService(db);

    var requestedStart = new DateTimeOffset(
        new DateTime(2025, 1, 1, 10, 17, 0, DateTimeKind.Utc));

    var req = new CreateBookingRequest(
        MasterId: masterId,
        ClientId: clientId,
        ServiceId: serviceId,
        StartUtc: requestedStart,
        IdempotencyKey: "idem-1");

    // Act
    var result = await svc.CreateAsync(req, CancellationToken.None);

    // Assert
    Assert.Equal(masterId, result.MasterId);
    Assert.Equal(clientId, result.ClientId);
    Assert.Equal(serviceId, result.ServiceId);
    Assert.Equal(BookingStatus.Pending, result.Status);

    var expectedStart = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
    Assert.Equal(expectedStart, result.StartUtc);

    Assert.Equal(expectedStart.AddMinutes(service.DurationMinutes), result.EndUtc);

    var booking = await db.Bookings.SingleAsync();
    Assert.Equal(result.Id, booking.Id);
    Assert.Equal(expectedStart, booking.StartUtc);
    Assert.Equal(expectedStart.AddMinutes(service.DurationMinutes), booking.EndUtc);
    Assert.Equal("idem-1", booking.IdempotencyKey);
    Assert.Equal(BookingStatus.Pending, booking.Status);
  }

  [Fact]
  public async Task CreateAsync_SameIdempotencyKey_ReturnsExistingBookingAndDoesNotCreateDuplicate()
  {
    // Arrange
    await using var db = CreateDbContext();

    var masterUserId = Guid.NewGuid();
    var masterId = Guid.NewGuid();
    var clientId = Guid.NewGuid();
    var serviceId = Guid.NewGuid();

    var masterUser = new User
    {
      Id = masterUserId,
      FirstName = "John",
      LastName = "Master"
    };

    var client = new User
    {
      Id = clientId,
      FirstName = "Alice",
      LastName = "Client"
    };

    var master = new Master
    {
      Id = masterId,
      UserId = masterUserId,
      User = masterUser
    };

    var service = new Service
    {
      Id = serviceId,
      MasterId = masterId,
      DurationMinutes = 60,
      Name = "Haircut"
    };

    db.Users.Add(masterUser);
    db.Users.Add(client);
    db.Masters.Add(master);
    db.Services.Add(service);
    await db.SaveChangesAsync();

    var svc = new BookingService(db);

    var requestedStart = DateTimeOffset.UtcNow.AddDays(1);
    const string idemKey = "same-idem-key";

    var req = new CreateBookingRequest(
        MasterId: masterId,
        ClientId: clientId,
        ServiceId: serviceId,
        StartUtc: requestedStart,
        IdempotencyKey: idemKey);

    // Act
    var first = await svc.CreateAsync(req, CancellationToken.None);
    var second = await svc.CreateAsync(req, CancellationToken.None);

    // Assert
    Assert.Equal(first.Id, second.Id);
    Assert.Equal(first.StartUtc, second.StartUtc);
    Assert.Equal(first.EndUtc, second.EndUtc);

    var count = await db.Bookings.CountAsync();
    Assert.Equal(1, count);
  }

  [Fact]
  public async Task CreateAsync_OverlappingExistingBooking_ThrowsInvalidOperationException()
  {
    await using var db = CreateDbContext();

    var masterUserId = Guid.NewGuid();
    var masterId = Guid.NewGuid();
    var clientId = Guid.NewGuid();
    var serviceId = Guid.NewGuid();

    var masterUser = new User
    {
      Id = masterUserId,
      FirstName = "John",
      LastName = "Master"
    };

    var client = new User
    {
      Id = clientId,
      FirstName = "Alice",
      LastName = "Client"
    };

    var master = new Master
    {
      Id = masterId,
      UserId = masterUserId,
      User = masterUser
    };

    var service = new Service
    {
      Id = serviceId,
      MasterId = masterId,
      DurationMinutes = 60,
      Name = "Haircut"
    };

    db.Users.Add(masterUser);
    db.Users.Add(client);
    db.Masters.Add(master);
    db.Services.Add(service);

    var existingStart = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
    var existingEnd = existingStart.AddMinutes(service.DurationMinutes);

    var existing = new Booking
    {
      Id = Guid.NewGuid(),
      MasterId = masterId,
      ClientId = clientId,
      ServiceId = serviceId,
      Status = BookingStatus.Confirmed,
      StartUtc = existingStart,
      EndUtc = existingEnd,
      HoldExpiresUtc = DateTime.UtcNow.AddDays(1), // ще актуальна
      IdempotencyKey = "existing",
      CreatedAtUtc = DateTime.UtcNow
    };

    db.Bookings.Add(existing);
    await db.SaveChangesAsync();

    var svc = new BookingService(db);

    var requestedStart = new DateTimeOffset(
        new DateTime(2025, 1, 1, 10, 10, 0, DateTimeKind.Utc));

    var req = new CreateBookingRequest(
        MasterId: masterId,
        ClientId: clientId,
        ServiceId: serviceId,
        StartUtc: requestedStart,
        IdempotencyKey: "overlap-test");

    // Act
    var ex = await Assert.ThrowsAsync<InvalidOperationException>(
        () => svc.CreateAsync(req, CancellationToken.None));

    // Assert
    Assert.Equal("Slot overlaps an existing booking.", ex.Message);

    var count = await db.Bookings.CountAsync();
    Assert.Equal(1, count);
  }

}
