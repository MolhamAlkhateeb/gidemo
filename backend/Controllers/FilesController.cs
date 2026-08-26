using Chatbot.Api.Data;
using Chatbot.Api.Domain;
using Chatbot.Api.Infrastructure;
using Chatbot.Api.Models;
using Chatbot.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chatbot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FilesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IStorageService _storage;

    public FilesController(AppDbContext db, IStorageService storage)
    {
        _db = db;
        _storage = storage;
    }

    /// <summary>Issues a presigned S3 PUT URL and records file metadata.</summary>
    [HttpPost("presign")]
    public async Task<ActionResult<PresignUploadResponse>> Presign(PresignUploadRequest req, CancellationToken ct)
    {
        var userId = HttpContext.GetUserId();
        var key = _storage.BuildKey(userId, req.FileName);

        var file = new StoredFile
        {
            UserId = userId,
            FileName = req.FileName,
            ContentType = req.ContentType,
            SizeBytes = req.SizeBytes,
            S3Key = key,
            Kind = ClassifyKind(req.ContentType, req.FileName),
            IsOutput = false
        };
        _db.Files.Add(file);
        await _db.SaveChangesAsync(ct);

        var url = await _storage.PresignPutAsync(key, req.ContentType, TimeSpan.FromMinutes(15));
        return Ok(new PresignUploadResponse(file.Id, url, key));
    }

    /// <summary>Returns a short-lived download URL for a stored artifact.</summary>
    [HttpGet("{id:guid}/url")]
    public async Task<ActionResult<object>> GetUrl(Guid id, CancellationToken ct)
    {
        var userId = HttpContext.GetUserId();
        var file = await _db.Files.FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId, ct);
        if (file is null) return NotFound();
        var url = await _storage.PresignGetAsync(file.S3Key, TimeSpan.FromMinutes(15));
        return Ok(new { url, file.FileName, file.ContentType });
    }

    /// <summary>Streams a stored object directly (used to render generated images inline).</summary>
    [HttpGet("{id:guid}/raw")]
    public async Task<IActionResult> GetRaw(Guid id, CancellationToken ct)
    {
        var userId = HttpContext.GetUserId();
        var file = await _db.Files.FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId, ct);
        if (file is null) return NotFound();
        var stream = await _storage.GetObjectAsync(file.S3Key, ct);
        return File(stream, file.ContentType);
    }

    private static ModalityKind ClassifyKind(string contentType, string fileName)
    {
        if (contentType.StartsWith("image/")) return ModalityKind.Image;
        if (contentType.StartsWith("video/")) return ModalityKind.Video;
        if (contentType.StartsWith("audio/")) return ModalityKind.Audio;
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext is ".docx" or ".xlsx" or ".pdf" or ".txt"
            ? ModalityKind.Document
            : ModalityKind.Document;
    }
}
