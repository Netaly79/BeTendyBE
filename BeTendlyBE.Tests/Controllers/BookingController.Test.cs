using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

using BeTendlyBE.Controllers;
using BeTendlyBE.Services;
using BeTendlyBE.Domain;

namespace BeTendlyBE.Tests.Controllers;

public class BookingControllerTests
{
    [Fact]
    public async Task Create_ValidRequest_ReturnsCreatedWithBookingResponse()
    {
        // Arrange
        var mockSvc = new Mock<IBookingService>();

        var expectedBooking = new BookingResponse(
            Id: Guid.NewGuid(),
            MasterId: Guid.NewGuid(),
            ClientId: Guid.NewGuid(),
            ServiceId: Guid.NewGuid(),
            MasterName: "John Master",
            ClientName: "Bob Client",
            ServiceName: "Haircut",
            Status: BookingStatus.Pending,
            StartUtc: DateTime.UtcNow.AddHours(1),
            EndUtc: DateTime.UtcNow.AddHours(2),
            CreatedAtUtc: DateTime.UtcNow,
            HoldExpiresUtc: null
        );

        mockSvc
            .Setup(s => s.CreateAsync(It.IsAny<CreateBookingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedBooking);

        var controller = new BookingController(mockSvc.Object);

        var req = new CreateBookingRequest(
            MasterId: expectedBooking.MasterId,
            ClientId: expectedBooking.ClientId,
            ServiceId: expectedBooking.ServiceId,
            StartUtc: expectedBooking.StartUtc,
            IdempotencyKey: Guid.NewGuid().ToString()
        );

        // Act
        var result = await controller.Create(req, CancellationToken.None);

        // Assert
        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(BookingController.GetById), created.ActionName);

        // Assert
        var body = Assert.IsType<BookingResponse>(created.Value);
        Assert.Equal(expectedBooking.Id, body.Id);
        Assert.Equal(expectedBooking.MasterId, body.MasterId);
        Assert.Equal(expectedBooking.ClientId, body.ClientId);
        Assert.Equal(expectedBooking.ServiceId, body.ServiceId);
        Assert.Equal(expectedBooking.Status, body.Status);

        mockSvc.Verify(s => s.CreateAsync(req, It.IsAny<CancellationToken>()), Times.Once);
    }
}
