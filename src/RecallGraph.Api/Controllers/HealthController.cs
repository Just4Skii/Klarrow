using Microsoft.AspNetCore.Mvc;

namespace RecallGraph.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class HealthController : ControllerBase
{
    [HttpGet("health")]
    public ActionResult<object> Health() => Ok(new { status = "ok", time = DateTime.UtcNow, service = "RecallGraph" });
}
