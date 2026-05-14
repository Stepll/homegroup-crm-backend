using HomeGroup.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeGroup.API.Controllers;

[ApiController]
[Route("api/v1/google-calendar")]
[Authorize]
public class GoogleCalendarController(GoogleCalendarSyncService syncService) : ControllerBase
{
    [HttpPost("sync")]
    public async Task<IActionResult> Sync()
    {
        try
        {
            var count = await syncService.SyncAsync();
            return Ok(new { synced = count });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Sync failed: " + ex.Message });
        }
    }
}
