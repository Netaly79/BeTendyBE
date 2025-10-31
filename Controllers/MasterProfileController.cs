using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using BeTendyBE.Contracts;
using BeTendyBE.Data;
using BeTendyBE.Domain;
using BeTendyBE.Infrastructure.Identity;

namespace BeTendyBE.Controllers;

[ApiController]
[Route("api/master/profile")]
[Authorize(Roles = "Master")]
public sealed class MasterProfileController : ControllerBase
{
    private readonly AppDbContext _db;
    public MasterProfileController(AppDbContext db) => _db = db;

    // GET /api/master/profile
    [HttpGet]
    public async Task<ActionResult<MasterProfileResponse>> Get(CancellationToken ct)
    {
        var userId = User.GetUserId();

        var role = await _db.Users.Where(u => u.Id == userId).Select(u => u.Role).FirstOrDefaultAsync(ct);
        if (role != UserRole.Master) return Forbid();

        var mp = await _db.Masters.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId, ct);
        var resp = mp is null
            ? new MasterProfileResponse(null, null, null, null)
            : new MasterProfileResponse(mp.About, mp.Skills, mp.ExperienceYears, mp.Address);

        return Ok(resp);
    }

    // PUT /api/master/profile  (upsert)
    [HttpPut]
    public async Task<ActionResult<MasterProfileResponse>> Upsert([FromBody] UpsertMasterProfileRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return NotFound();
        if (user.Role != UserRole.Master) return Forbid();

        var about  = string.IsNullOrWhiteSpace(req.About)  ? null : req.About.Trim();
        var skills = string.IsNullOrWhiteSpace(req.Skills) ? null : req.Skills.Trim();

        var mp = await _db.Masters.FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (mp is null)
        {
            mp = new Master { UserId = userId };
            _db.Masters.Add(mp);
        }

        mp.About = about;
        mp.Skills = skills;
        mp.ExperienceYears = req.ExperienceYears;
        mp.Address = string.IsNullOrWhiteSpace(req.Address) ? null : req.Address.Trim();

        await _db.SaveChangesAsync(ct);

        return Ok(new MasterProfileResponse(mp.About, mp.Skills, mp.ExperienceYears, mp.Address));
    }
}
