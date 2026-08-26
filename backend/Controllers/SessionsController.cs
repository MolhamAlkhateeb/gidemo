using Chatbot.Api.Data;
using Chatbot.Api.Domain;
using Chatbot.Api.Infrastructure;
using Chatbot.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chatbot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SessionsController : ControllerBase
{
    private readonly AppDbContext _db;

    public SessionsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ChatSession>>> List(CancellationToken ct)
    {
        var userId = HttpContext.GetUserId();
        var sessions = await _db.Sessions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.UpdatedAt)
            .ToListAsync(ct);
        return Ok(sessions);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ChatSession>> Get(Guid id, CancellationToken ct)
    {
        var userId = HttpContext.GetUserId();
        var session = await _db.Sessions
            .Include(s => s.Messages.OrderBy(m => m.CreatedAt))
            .ThenInclude(m => m.Attachments)
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct);
        return session is null ? NotFound() : Ok(session);
    }

    [HttpPost]
    public async Task<ActionResult<ChatSession>> Create(CreateSessionRequest req, CancellationToken ct)
    {
        var session = new ChatSession
        {
            UserId = HttpContext.GetUserId(),
            Title = string.IsNullOrWhiteSpace(req.Title) ? "New chat" : req.Title!,
            ModelId = req.ModelId,
            AutomaticModel = req.AutomaticModel
        };
        _db.Sessions.Add(session);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = session.Id }, session);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = HttpContext.GetUserId();
        var session = await _db.Sessions.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct);
        if (session is null) return NotFound();
        _db.Sessions.Remove(session);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
