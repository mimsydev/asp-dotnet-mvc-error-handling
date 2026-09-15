using Microsoft.AspNetCore.Mvc;

namespace ErrorHandling.Controllers;

[ApiController]
[Route("/health")]
public class HealthController : Controller
{
    public async Task<IActionResult> Health()
    {
        return Ok("Healthy"); 
    }
}
