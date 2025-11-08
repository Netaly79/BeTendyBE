using BeTendyBE.Data;
using BeTendyBE.Domain;
using BeTendyBE.DTO;
using BeTendyBE.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;
using Swashbuckle.AspNetCore.Filters;

namespace BeTendyBE.Controllers;

[ApiController]
[Route("services")]
[Consumes("application/json")]
[Produces("application/json")]
[Authorize]
public sealed class ServicesController : ControllerBase
{
    private readonly AppDbContext _db;
    public ServicesController(AppDbContext db) => _db = db;

    /// <summary>
    /// Додати послугу (майстер)
    /// </summary>
    /// <remarks>
    /// Потребує ролі <c>Master</c>.
    /// Створює послугу та повертає 201 та нову послугу.
    /// </remarks>
    /// <response code="201">Послугу створено.</response>
    /// <response code="400">Помилка валідації вхідних даних.</response>
    /// <response code="401">Користувач неавторизований.</response>
    /// <response code="403">У користувача немає профілю майстра.</response>
    [HttpPost]
    [SwaggerOperation(
        Summary = "Додати послугу (майстер)",
        Description = "Створює нову послугу, прив’язану до поточного майстра."
    )]
    [ProducesResponseType(typeof(CreateServiceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<object>> Create([FromBody] CreateServiceRequest req, CancellationToken ct)
    {

        var userId = User.GetUserId();

        var master = await _db.Set<Master>()
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.UserId == userId, ct);

        if (master is null)
            return Forbid("Only a master can create services (master profile not found).");

        var entity = new Service
        {
            Id = Guid.NewGuid(),
            MasterId = master.Id,
            Name = req.Name.Trim(),
            Price = req.Price,
            DurationMinutes = req.DurationMinutes,
            Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Services.Add(entity);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, new
        {
            entity.Id,
            entity.MasterId,
            entity.Name,
            entity.Price,
            entity.DurationMinutes,
            entity.Description,
            entity.CreatedAtUtc
        });
    }

  /// <summary>
  /// Отримати послугу за ID
  /// </summary>
  /// <remarks>
  /// Повертає інформацію про послугу, якщо вона існує.
  /// </remarks>
  /// <param name="id">ID послуги</param>
  /// <param name="ct"></param>
  /// <response code="200">Послугу знайдено та повернуто.</response>
  /// <response code="404">Послугу не знайдено.</response>
  /// <response code="401">Користувач неавторизований.</response>
  /// <response code="403">У користувача немає доступу.</response>
  [HttpGet("{id:guid}")]
    [SwaggerOperation(
        Summary = "Отримати послугу за ID",
        Description = "Повертає детальну інформацію про послугу за вказаним ідентифікатором."
    )]

    [SwaggerResponse(200, "Successful get service", typeof(CreateServiceResponse))]
    [SwaggerResponseExample(200, typeof(ServiceResponse200Example))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<Service>> GetById(Guid id, CancellationToken ct)
    {
        var service = await _db.Services.FindAsync(new object[] { id }, ct);
        return service is null ? NotFound() : Ok(service);
    }

  /// <summary>
  /// Оновити послугу (майстер)
  /// </summary>
  /// <remarks>
  /// Потребує ролі <c>Master</c>. Оновлює поля та повертає оновлену послугу.
  /// </remarks>
  /// <param name="id">ID послуги</param>
  /// <param name="req"></param>
  /// <param name="ct"></param>
  /// <response code="200">Послугу оновлено та повернуто.</response>
  /// <response code="400">Помилка валідації вхідних даних.</response>
  /// <response code="401">Користувач неавторизований.</response>
  /// <response code="403">Немає доступу до цієї послуги.</response>
  /// <response code="404">Послугу не знайдено.</response>
  [HttpPut("{id:guid}")]
    [SwaggerOperation(
        Summary = "Оновити послугу (майстер)",
        Description = "Оновлює існуючу послугу та повертає актуальні дані."
    )]

    [SwaggerRequestExample(typeof(UpdateServiceRequest), typeof(UpdateServiceRequestExample))]
    [SwaggerResponse(200, "Successful update", typeof(Service))]
    [SwaggerResponseExample(200, typeof(UpdateService200Example))]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Service>> Update(Guid id, [FromBody] UpdateServiceRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var master = await _db.Set<Master>()
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.UserId == userId, ct);

        if (master is null)
            return Forbid("Only a master can update services (master profile not found).");

        var entity = await _db.Services.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (entity is null)
            return NotFound();

        if (entity.MasterId != master.Id)
            return Forbid("You can update only your own services.");

        if (string.IsNullOrWhiteSpace(req.Name))
            return ValidationProblem("Name is required.");
        if (req.Price < 0)
            return ValidationProblem("Price must be non-negative.");
        if (req.DurationMinutes <= 0)
            return ValidationProblem("DurationMinutes must be positive.");

        entity.Name = req.Name.Trim();
        entity.Price = req.Price;
        entity.DurationMinutes = req.DurationMinutes;
        entity.Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim();

        await _db.SaveChangesAsync(ct);

        return Ok(entity);
    }

  /// <summary>
  /// Видалити послугу (майстер)
  /// </summary>
  /// <remarks>
  /// Потребує ролі <c>Master</c>. Видаляє послугу, якщо вона належить цьому майстру.
  /// </remarks>
  /// <param name="id">ID послуги</param>
  /// <param name="ct"></param>
  /// <response code="204">Послугу успішно видалено.</response>
  /// <response code="401">Користувач неавторизований.</response>
  /// <response code="403">Немає доступу до цієї послуги.</response>
  /// <response code="404">Послугу не знайдено.</response>
  [HttpDelete("{id:guid}")]
    [SwaggerOperation(
        Summary = "Видалити послугу (майстер)",
        Description = "Видаляє існуючу послугу, якщо вона належить майстру."
    )]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var master = await _db.Set<Master>()
            .FirstOrDefaultAsync(m => m.UserId == userId, ct);

        if (master is null)
            return Forbid("Only a master can delete services.");

        var service = await _db.Services.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (service is null)
            return NotFound();

        if (service.MasterId != master.Id)
            return Forbid("You can delete only your own services.");

        _db.Services.Remove(service);
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }
}
