using HomeGroup.API.Data;
using HomeGroup.API.Models.DTOs.Calendar;
using HomeGroup.API.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomeGroup.API.Controllers;

[ApiController]
[Route("api/v1/rooms")]
[Authorize]
public class RoomsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<RoomDto>>> GetAll() =>
        Ok(await db.Rooms.OrderBy(r => r.Name).Select(r => new RoomDto(r.Id, r.Name)).ToListAsync());

    [HttpPost]
    public async Task<ActionResult<RoomDto>> Create(RoomRequest request)
    {
        var room = new Room { Name = request.Name.Trim() };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();
        return Ok(new RoomDto(room.Id, room.Name));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<RoomDto>> Update(long id, RoomRequest request)
    {
        var room = await db.Rooms.FindAsync(id);
        if (room is null) return NotFound();
        room.Name = request.Name.Trim();
        await db.SaveChangesAsync();
        return Ok(new RoomDto(room.Id, room.Name));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var room = await db.Rooms.FindAsync(id);
        if (room is null) return NotFound();
        db.Rooms.Remove(room);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
