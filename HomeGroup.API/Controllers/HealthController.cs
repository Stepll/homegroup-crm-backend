using HomeGroup.API.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomeGroup.API.Controllers;

[ApiController]
[Route("health")]
public class HealthController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync("SELECT 1");
            return Ok(new { status = "healthy", db = "ok", timestamp = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            return StatusCode(503, new { status = "unhealthy", db = "error", error = ex.Message, timestamp = DateTime.UtcNow });
        }
    }
}
