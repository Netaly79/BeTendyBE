using BeTendlyBE.Data;
using BeTendlyBE.Domain;
using Microsoft.EntityFrameworkCore;

public sealed class MasterService : IMasterService
{
    private readonly AppDbContext _db;
    public MasterService(AppDbContext db) => _db = db;

    public async Task EnsureMasterAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
                   ?? throw new KeyNotFoundException("User not found");

        if (user.IsMaster)
            return;

        user.IsMaster = true;

        var exists = await _db.Masters.AnyAsync(m => m.UserId == userId, ct);
        if (!exists)
            _db.Masters.Add(new Master { UserId = userId });

        await _db.SaveChangesAsync(ct);
    }
}