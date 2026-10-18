using Microsoft.AspNetCore.Mvc;
using RecallGraph.Application;
using RecallGraph.Infrastructure;

namespace RecallGraph.Api.Controllers;

[ApiController]
[Route("api/capabilities")]
public sealed class CapabilitiesController(MetaCapabilityService capabilities) : ControllerBase
{
    [HttpGet]
    public ActionResult<List<CapabilityStatusDto>> Get() => Ok(capabilities.GetStatuses());
}