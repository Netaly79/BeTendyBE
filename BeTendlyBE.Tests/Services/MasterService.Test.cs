using Microsoft.EntityFrameworkCore;
using Xunit;

using BeTendlyBE.Data;
using BeTendlyBE.Domain;

namespace BeTendlyBE.Tests.Services;

public class MasterServiceTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"master_service_{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task EnsureMasterAsync_UserNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        await using var db = CreateDbContext();
        var svc = new MasterService(db);

        var nonExistingUserId = Guid.NewGuid();

        // Act
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.EnsureMasterAsync(nonExistingUserId, CancellationToken.None));

        // Assert
        Assert.Equal("User not found", ex.Message);
    }

    [Fact]
    public async Task EnsureMasterAsync_UserIsNotMasterAndNoMasterRecord_CreatesMasterAndSetsFlag()
    {
        // Arrange
        await using var db = CreateDbContext();

        var userId = Guid.NewGuid();

        var user = new User
        {
            Id = userId,
            FirstName = "John",
            LastName = "Doe",
            IsMaster = false
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var svc = new MasterService(db);

        // Act
        await svc.EnsureMasterAsync(userId, CancellationToken.None);

        // Assert
        var dbUser = await db.Users.SingleAsync();
        Assert.True(dbUser.IsMaster);


        var masters = await db.Masters.ToListAsync();
        Assert.Single(masters);
        Assert.Equal(userId, masters[0].UserId);
    }

    [Fact]
    public async Task EnsureMasterAsync_UserIsNotMasterButMasterRecordExists_OnlySetsFlag_NoDuplicateMaster()
    {
        // Arrange
        await using var db = CreateDbContext();

        var userId = Guid.NewGuid();

        var user = new User
        {
            Id = userId,
            FirstName = "Jane",
            LastName = "Smith",
            IsMaster = false
        };

        var existingMaster = new Master
        {
            Id = Guid.NewGuid(),
            UserId = userId
        };

        db.Users.Add(user);
        db.Masters.Add(existingMaster);
        await db.SaveChangesAsync();

        var svc = new MasterService(db);

        // Act
        await svc.EnsureMasterAsync(userId, CancellationToken.None);

        // Assert
        var dbUser = await db.Users.SingleAsync();
        Assert.True(dbUser.IsMaster);

        var masters = await db.Masters.ToListAsync();
        Assert.Single(masters);
        Assert.Equal(existingMaster.Id, masters[0].Id);
        Assert.Equal(userId, masters[0].UserId);
    }
}
