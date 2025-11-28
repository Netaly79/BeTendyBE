using System.Security.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Moq;
using Xunit;

using BeTendlyBE.Data;
using BeTendlyBE.Domain;
using BeTendlyBE.Services;
using BeTendlyBE.Contracts;

namespace BeTendlyBE.Tests.Services;

public class ProfileServiceTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"profile_service_{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task GetMeAsync_UserExists_ReturnsUser()
    {
        // Arrange
        await using var db = CreateDbContext();

        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            FirstName = "John",
            LastName = "Doe",
            IsMaster = true,
            Master = new Master { Id = Guid.NewGuid(), UserId = userId }
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var hasher = new Mock<IPasswordHasher<User>>();
        var svc = new ProfileService(db, hasher.Object);

        // Act
        var result = await svc.GetMeAsync(userId, CancellationToken.None);

        // Assert
        Assert.Equal(userId, result.Id);
        Assert.Equal("John", result.FirstName);
        Assert.NotNull(result.Master); // Include Master відпрацював
    }

    [Fact]
    public async Task GetMeAsync_UserNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        await using var db = CreateDbContext();
        var hasher = new Mock<IPasswordHasher<User>>();
        var svc = new ProfileService(db, hasher.Object);

        var missingUserId = Guid.NewGuid();

        // Act
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.GetMeAsync(missingUserId, CancellationToken.None));

        // Assert
        Assert.Equal("User not found", ex.Message);
    }

    [Fact]
    public async Task UpdateAsync_ValidRequest_UpdatesUserFieldsAndSaves()
    {
        // Arrange
        await using var db = CreateDbContext();

        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            FirstName = " Old ",
            LastName = " Name ",
            Phone = " 000 ",
            Email = "user@example.com"
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var hasher = new Mock<IPasswordHasher<User>>();
        var svc = new ProfileService(db, hasher.Object);

        var req = new UpdateClientProfileRequest(
        FirstName: "  John  ",
        LastName:  "  Doe  ",
        Phone:     "  +380991112233  ");
        
        // Act
        var result = await svc.UpdateAsync(userId, req, CancellationToken.None);


        Assert.Equal("John", result.FirstName);
        Assert.Equal("Doe", result.LastName);
        Assert.Equal("+380991112233", result.Phone);

      
        var dbUser = await db.Users.SingleAsync();
        Assert.Equal("John", dbUser.FirstName);
        Assert.Equal("Doe", dbUser.LastName);
        Assert.Equal("+380991112233", dbUser.Phone);
    }

    [Fact]
    public async Task ChangePasswordAsync_ValidCurrentPassword_UpdatesPasswordHash()
    {
        // Arrange
        await using var db = CreateDbContext();

        var userId = Guid.NewGuid();
        var oldHash = "HASHED-OLD";
        var newHash = "HASHED-NEW";

        var user = new User
        {
            Id = userId,
            Email = "user@example.com",
            PasswordHash = oldHash
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var hasher = new Mock<IPasswordHasher<User>>();

        hasher
            .Setup(h => h.VerifyHashedPassword(
                It.IsAny<User>(),
                oldHash,
                "old-pass"))
            .Returns(PasswordVerificationResult.Success);

        hasher
            .Setup(h => h.HashPassword(It.IsAny<User>(), "new-pass"))
            .Returns(newHash);

        var svc = new ProfileService(db, hasher.Object);

        // Act
        await svc.ChangePasswordAsync(userId, "old-pass", "new-pass", CancellationToken.None);

        // Assert
        var dbUser = await db.Users.SingleAsync();
        Assert.Equal(newHash, dbUser.PasswordHash);

        hasher.Verify(h => h.VerifyHashedPassword(
                It.IsAny<User>(),
                oldHash,
                "old-pass"),
            Times.Once);

        hasher.Verify(h => h.HashPassword(
                It.IsAny<User>(),
                "new-pass"),
            Times.Once);
    }

    [Fact]
    public async Task ChangePasswordAsync_InvalidCurrentPassword_ThrowsAuthenticationExceptionAndDoesNotChangeHash()
    {
        // Arrange
        await using var db = CreateDbContext();

        var userId = Guid.NewGuid();
        var oldHash = "HASHED-OLD";

        var user = new User
        {
            Id = userId,
            Email = "user@example.com",
            PasswordHash = oldHash
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var hasher = new Mock<IPasswordHasher<User>>();

        hasher
            .Setup(h => h.VerifyHashedPassword(
                It.IsAny<User>(),
                oldHash,
                "wrong-pass"))
            .Returns(PasswordVerificationResult.Failed);

        var svc = new ProfileService(db, hasher.Object);

        // Act
        var ex = await Assert.ThrowsAsync<AuthenticationException>(() =>
            svc.ChangePasswordAsync(userId, "wrong-pass", "new-pass", CancellationToken.None));

        // Assert
        Assert.Equal("Current password is invalid.", ex.Message);

        var dbUser = await db.Users.SingleAsync();
        Assert.Equal(oldHash, dbUser.PasswordHash);

        hasher.Verify(h => h.HashPassword(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }
}
