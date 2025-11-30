using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace BeTendlyBE.Tests.Services;

public class EmailServiceTests
{
    [Fact]
    public void Ctor_NullClient_ThrowsArgumentNullException()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:SenderAddress"] = "no-reply@betendly.test"
            })
            .Build();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(() =>
        {
            var _ = new EmailService(null!, config);
        });
    }

    [Fact]
    public void Ctor_MissingSenderAddress_ThrowsInvalidOperationException()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var client = new Mock<EmailClient>().Object;

        // Act + Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            var _ = new EmailService(client, config);
        });

        Assert.Equal("Email:SenderAddress is not configured", ex.Message);
    }

    [Fact]
    public async Task SendResetPasswordEmailAsync_ValidInput_CallsClientSendAsync()
    {
        // Arrange
        var senderAddress = "no-reply@betendly.test";

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:SenderAddress"] = senderAddress
            })
            .Build();

        var mockClient = new Mock<EmailClient>();

        mockClient
            .Setup(c => c.SendAsync(
                It.IsAny<WaitUntil>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<EmailSendOperation>());

        var service = new EmailService(mockClient.Object, config);

        var toEmail = "user@example.com";
        var resetLink = "https://betendly.test/reset?token=abc";

        // Act
        await service.SendResetPasswordEmailAsync(toEmail, resetLink);

        mockClient.Verify(c => c.SendAsync(
                WaitUntil.Completed,
                senderAddress,
                toEmail,
                "Скидання паролю в BeTendly",
                It.Is<string>(body =>
                    body.Contains(resetLink) &&
                    body.Contains("Вітаємо!")
                ),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendResetPasswordEmailAsync_ClientThrows_RequestFailedException_IsCaught()
    {
        // Arrange
        var senderAddress = "no-reply@betendly.test";

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:SenderAddress"] = senderAddress
            })
            .Build();

        var mockClient = new Mock<EmailClient>();

        mockClient
            .Setup(c => c.SendAsync(
                It.IsAny<WaitUntil>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(500, "Something went wrong"));

        var service = new EmailService(mockClient.Object, config);

        // Act + Assert
        var exception = await Record.ExceptionAsync(() =>
            service.SendResetPasswordEmailAsync("user@example.com", "https://link"));

        Assert.Null(exception);
    }
}
