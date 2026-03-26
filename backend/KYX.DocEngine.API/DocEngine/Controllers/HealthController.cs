using Microsoft.AspNetCore.Mvc;

namespace KYX.DocEngine.API.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "ok", service = "docengine" });
}
