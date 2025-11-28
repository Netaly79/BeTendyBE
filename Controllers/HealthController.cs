using Microsoft.AspNetCore.Mvc;

namespace BeTendlyBE.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    [HttpGet] public IActionResult Get() => Ok(new { status = "ok", time = DateTime.UtcNow });
}

