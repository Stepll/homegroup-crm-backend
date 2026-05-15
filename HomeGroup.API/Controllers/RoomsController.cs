using HomeGroup.API.Authorization;
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
    private static RoomDto ToDto(Room r) => new(r.Id, r.Name, r.Building, r.Floor, r.Color);

    [HttpGet]
    public async Task<ActionResult<List<RoomDto>>> GetAll() =>
        Ok(await db.Rooms
            .OrderBy(r => r.Building).ThenBy(r => r.Floor).ThenBy(r => r.Name)
            .Select(r => new RoomDto(r.Id, r.Name, r.Building, r.Floor, r.Color))
            .ToListAsync());

    [HttpGet("{id}")]
    public async Task<ActionResult<RoomDto>> GetById(long id)
    {
        var room = await db.Rooms.FindAsync(id);
        return room is null ? NotFound() : Ok(ToDto(room));
    }

    [HttpPost]
    [RequirePermission("settings.rooms")]
    public async Task<ActionResult<RoomDto>> Create(RoomRequest request)
    {
        var room = new Room
        {
            Name = request.Name.Trim(),
            Building = request.Building,
            Floor = request.Floor,
            Color = request.Color,
        };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();
        return Ok(ToDto(room));
    }

    [HttpPut("{id}")]
    [RequirePermission("settings.rooms")]
    public async Task<ActionResult<RoomDto>> Update(long id, RoomRequest request)
    {
        var room = await db.Rooms.FindAsync(id);
        if (room is null) return NotFound();
        room.Name = request.Name.Trim();
        room.Building = request.Building;
        room.Floor = request.Floor;
        room.Color = request.Color;
        await db.SaveChangesAsync();
        return Ok(ToDto(room));
    }

    [HttpDelete("{id}")]
    [RequirePermission("settings.rooms")]
    public async Task<IActionResult> Delete(long id)
    {
        var room = await db.Rooms.FindAsync(id);
        if (room is null) return NotFound();
        db.Rooms.Remove(room);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
