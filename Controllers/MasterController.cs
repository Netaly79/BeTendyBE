using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using BeTendyBE.Data;
using BeTendyBE.Domain;
using BeTendyBE.Infrastructure.Identity;

namespace BeTendyBE.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MasterController : ControllerBase
{
    private readonly AppDbContext _db;
    public MasterController(AppDbContext db) => _db = db;

    [HttpPost("upgrade")]
    [Authorize]
    public async Task<IActionResult> Upgrade(CancellationToken ct)
    {
        var userId = ClaimsPrincipalExt.GetUserId(User);

        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (u is null) return NotFound();

        if (u.Role == UserRole.Master) return NoContent(); // уже мастер

        u.Role = UserRole.Master;

        if (!await _db.MasterProfiles.AnyAsync(m => m.UserId == u.Id, ct))
            _db.MasterProfiles.Add(new MasterProfile { UserId = u.Id });

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{userId:guid}/approve")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Approve(Guid userId, CancellationToken ct)
    {
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (u is null) return NotFound();

        u.Role = UserRole.Master;
        if (!await _db.MasterProfiles.AnyAsync(m => m.UserId == u.Id, ct))
            _db.MasterProfiles.Add(new MasterProfile { UserId = u.Id });

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
