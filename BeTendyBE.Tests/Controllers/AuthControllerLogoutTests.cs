// Comprehensive tests for the AuthController Logout method
// Tests all scenarios: valid token, invalid token, empty token, different user token, exceptions, etc.

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

using BeTendyBE.Controllers;
using BeTendyBE.Data;
using BeTendyBE.Domain;
using BeTendyBE.Infrastructure.Identity;
using BeTendlyBE.Services;

namespace BeTendyBE.Tests.Controllers;

/// <summary>
/// Test class for the AuthController.Logout method.
/// Covers all major scenarios including success cases, validation errors, unauthorized access, and exception handling.
/// </summary>
public class AuthControllerLogoutTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Mock<IPasswordHasher<User>> _mockPasswordHasher;
    private readonly Mock<IJwtProvider> _mockJwtProvider;
    private readonly Mock<IRefreshTokenService> _mockRefreshTokenService;
    private readonly Mock<IMasterService> _mockMasterService;
    private readonly AuthController _controller;
    private readonly User _testUser;
    private readonly RefreshToken _testRefreshToken;

    public AuthControllerLogoutTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);

        // Setup mocks
        _mockPasswordHasher = new Mock<IPasswordHasher<User>>();
        _mockJwtProvider = new Mock<IJwtProvider>();
        _mockRefreshTokenService = new Mock<IRefreshTokenService>();
        _mockMasterService = new Mock<IMasterService>();

        // Create controller
        _controller = new AuthController(
            _context,
            _mockPasswordHasher.Object,
            _mockJwtProvider.Object,
            _mockRefreshTokenService.Object,
            _mockMasterService.Object);

        // Setup test data
        _testUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            PasswordHash = "hashedpassword",
            IsMaster = false
        };

        _testRefreshToken = new RefreshToken
        {
            Id = 1,
            UserId = _testUser.Id,
            TokenHash = "test-token-hash",
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(13),
            DeviceInfo = "Test Device",
            User = _testUser
        };

        // Add test user to database
        _context.Users.Add(_testUser);
        _context.RefreshTokens.Add(_testRefreshToken);
        _context.SaveChanges();

        // Setup controller context with authenticated user
        SetupControllerContext(_testUser.Id);
    }

    private void SetupControllerContext(Guid userId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, _testUser.Email),
            new(ClaimTypes.GivenName, _testUser.FirstName),
            new(ClaimTypes.Surname, _testUser.LastName)
        };

        var identity = new ClaimsIdentity(claims, "Test");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext
        {
            User = claimsPrincipal,
            Request =
            {
                Path = "/auth/logout"
            }
        };

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public async Task Logout_WithValidRefreshToken_ReturnsNoContent()
    {
        // Arrange
        var request = new RefreshRequest { RefreshToken = "valid-refresh-token" };

        _mockRefreshTokenService
            .Setup(x => x.ValidateAsync(request.RefreshToken, default))
            .ReturnsAsync((_testUser, _testRefreshToken));

        _mockRefreshTokenService
            .Setup(x => x.RevokeAsync(_testRefreshToken, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Logout(request);

        // Assert
        Assert.IsType<NoContentResult>(result);

        // Verify that ValidateAsync was called with correct parameters
        _mockRefreshTokenService.Verify(
            x => x.ValidateAsync(request.RefreshToken, default),
            Times.Once);

        // Verify that RevokeAsync was called with correct token
        _mockRefreshTokenService.Verify(
            x => x.RevokeAsync(_testRefreshToken, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Logout_WithEmptyRefreshToken_ReturnsBadRequest()
    {
        // Arrange
        var request = new RefreshRequest { RefreshToken = "" };

        // Act
        var result = await _controller.Logout(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var problemDetails = Assert.IsType<ProblemDetails>(badRequestResult.Value);

        Assert.Equal("Refresh token required", problemDetails.Title);
        Assert.Equal(StatusCodes.Status400BadRequest, problemDetails.Status);
        Assert.Equal("The refresh token must be provided.", problemDetails.Detail);
        Assert.Equal("https://httpstatuses.io/400", problemDetails.Type);
        Assert.Equal("/auth/logout", problemDetails.Instance);

        // Verify that refresh token service was never called
        _mockRefreshTokenService.Verify(
            x => x.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _mockRefreshTokenService.Verify(
            x => x.RevokeAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Logout_WithWhitespaceRefreshToken_ReturnsBadRequest()
    {
        // Arrange
        var request = new RefreshRequest { RefreshToken = "   " };

        // Act
        var result = await _controller.Logout(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var problemDetails = Assert.IsType<ProblemDetails>(badRequestResult.Value);

        Assert.Equal("Refresh token required", problemDetails.Title);
        Assert.Equal(StatusCodes.Status400BadRequest, problemDetails.Status);
    }

    [Fact]
    public async Task Logout_WithNullRefreshToken_ReturnsBadRequest()
    {
        // Arrange
        var request = new RefreshRequest { RefreshToken = null! };

        // Act
        var result = await _controller.Logout(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Logout_WithTokenFromDifferentUser_ReturnsUnauthorized()
    {
        // Arrange
        var differentUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "different@example.com",
            FirstName = "Different",
            LastName = "User"
        };

        var differentUserToken = new RefreshToken
        {
            Id = 2,
            UserId = differentUser.Id,
            TokenHash = "different-token-hash",
            User = differentUser
        };

        var request = new RefreshRequest { RefreshToken = "different-user-token" };

        _mockRefreshTokenService
            .Setup(x => x.ValidateAsync(request.RefreshToken, default))
            .ReturnsAsync((differentUser, differentUserToken));

        // Act
        var result = await _controller.Logout(request);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var problemDetails = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);

        Assert.Equal("Invalid refresh token", problemDetails.Title);
        Assert.Equal(StatusCodes.Status401Unauthorized, problemDetails.Status);
        Assert.Equal("The provided refresh token does not belong to the current user.", problemDetails.Detail);
        Assert.Equal("https://httpstatuses.io/401", problemDetails.Type);

        // Verify that RevokeAsync was never called for security reasons
        _mockRefreshTokenService.Verify(
            x => x.RevokeAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Logout_WithInvalidRefreshToken_ReturnsUnauthorized()
    {
        // Arrange
        var request = new RefreshRequest { RefreshToken = "invalid-refresh-token" };

        _mockRefreshTokenService
            .Setup(x => x.ValidateAsync(request.RefreshToken, default))
            .ThrowsAsync(new Exception("Invalid refresh token"));

        // Act
        var result = await _controller.Logout(request);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var problemDetails = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);

        Assert.Equal("Invalid refresh token", problemDetails.Title);
        Assert.Equal(StatusCodes.Status401Unauthorized, problemDetails.Status);
        Assert.Equal("The provided refresh token is invalid, expired, or already revoked.", problemDetails.Detail);
        Assert.Equal("https://httpstatuses.io/401", problemDetails.Type);

        // Verify that RevokeAsync was never called
        _mockRefreshTokenService.Verify(
            x => x.RevokeAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Logout_WhenRefreshServiceThrowsException_ReturnsUnauthorized()
    {
        // Arrange
        var request = new RefreshRequest { RefreshToken = "valid-refresh-token" };

        _mockRefreshTokenService
            .Setup(x => x.ValidateAsync(request.RefreshToken, default))
            .ReturnsAsync((_testUser, _testRefreshToken));

        _mockRefreshTokenService
            .Setup(x => x.RevokeAsync(_testRefreshToken, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.Logout(request);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var problemDetails = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);

        Assert.Equal("Invalid refresh token", problemDetails.Title);
        Assert.Equal(StatusCodes.Status401Unauthorized, problemDetails.Status);
        Assert.Equal("The provided refresh token is invalid, expired, or already revoked.", problemDetails.Detail);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}

/// <summary>
/// Tests for the RefreshRequest model class.
/// </summary>
public class RefreshRequestTests
{
    [Fact]
    public void RefreshRequest_HasCorrectDefaultValue()
    {
        // Arrange & Act
        var request = new RefreshRequest();

        // Assert
        Assert.Equal(string.Empty, request.RefreshToken);
    }

    [Fact]
    public void RefreshRequest_CanSetRefreshToken()
    {
        // Arrange
        var request = new RefreshRequest();
        var expectedToken = "test-refresh-token";

        // Act
        request.RefreshToken = expectedToken;

        // Assert
        Assert.Equal(expectedToken, request.RefreshToken);
    }
}