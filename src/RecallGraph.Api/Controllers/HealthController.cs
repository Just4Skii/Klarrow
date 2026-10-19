using Microsoft.AspNetCore.Mvc;

namespace RecallGraph.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class HealthController(IConfiguration configuration) : ControllerBase
{
    [HttpGet("health")]
    public ActionResult<object> Health()
    {
        var keyConfigured = !string.IsNullOrWhiteSpace(configuration["RecallGraph:ApiKey"]);
        return Ok(new
        {
            status = "ok",
            time = DateTime.UtcNow,
            service = "RecallGraph",
            accessControl = new
            {
                mode = keyConfigured ? "token" : "none",
                notice = keyConfigured
                    ? "API token access control enabled. Not enterprise identity/access management."
                    : "Local prototype mode: API access control not configured."
            }
        });
    }
}
