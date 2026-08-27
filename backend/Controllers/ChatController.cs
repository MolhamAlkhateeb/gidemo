using System.Text;
using System.Text.Json;
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
public class ChatController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IBedrockService _bedrock;
    private readonly IStorageService _storage;
    private readonly IDocumentParser _docParser;

    public ChatController(
        AppDbContext db,
        IBedrockService bedrock,
        IStorageService storage,
        IDocumentParser docParser)
    {
        _db = db;
        _bedrock = bedrock;
        _storage = storage;
        _docParser = docParser;
    }

    /// <summary>Streams a model response as Server-Sent Events and persists the turn.</summary>
    [HttpPost("stream")]
    public async Task Stream([FromBody] ChatRequest req, CancellationToken ct)
    {
        var userId = HttpContext.GetUserId();

        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        // Resolve or create the session.
        ChatSession session;
        if (req.SessionId is { } sid)
        {
            session = await _db.Sessions
                .Include(s => s.Messages)
                .FirstOrDefaultAsync(s => s.Id == sid && s.UserId == userId, ct)
                ?? throw new InvalidOperationException("Session not found");
        }
        else
        {
            session = new ChatSession
            {
                UserId = userId,
                Title = Truncate(req.Prompt, 60),
                ModelId = req.ModelId,
                AutomaticModel = req.ModelId == "automatic"
            };
            _db.Sessions.Add(session);
            await _db.SaveChangesAsync(ct);
            await WriteEvent("session", new { sessionId = session.Id }, ct);
        }

        // Build the effective prompt, appending extracted document text.
        var effectivePrompt = await BuildPromptAsync(userId, req, ct);

        // Resolve model (automatic routing) using the live catalog.
        var models = await _bedrock.ListModelsAsync(ct);
        var modelId = req.ModelId;
        if (modelId == "automatic")
        {
            var hasImageAttachment = req.AttachmentIds is { Length: > 0 } &&
                await _db.Files.AnyAsync(f => req.AttachmentIds.Contains(f.Id)
                    && f.UserId == userId && f.Kind == ModalityKind.Image, ct);

            modelId = await _bedrock.RouteAsync(effectivePrompt, models, hasImageAttachment, ct);
            await WriteEvent("routed", new { modelId }, ct);
        }
        var caps = models.FirstOrDefault(m => m.Id == modelId)?.Capabilities
            ?? ModelCapabilityMap.Resolve(modelId).Caps;

        // Persist the user message.
        var userMessage = new ChatMessage
        {
            SessionId = session.Id,
            Role = MessageRole.User,
            Content = req.Prompt
        };
        _db.Messages.Add(userMessage);
        await _db.SaveChangesAsync(ct);

        try
        {
            // Image-generation models (e.g. Nova Canvas) use InvokeModel, not streaming.
            if (caps.ImageOutput && !caps.TextOutput)
            {
                await GenerateImageAsync(userId, session, modelId, effectivePrompt, ct);
                return;
            }

            if (!caps.TextOutput)
            {
                await WriteEvent("error",
                    new { message = "This model does not support chat responses.", modelId }, ct);
                return;
            }

            // Collect the reply, streaming when supported and falling back to a single call otherwise.
            var images = caps.ImageInput
                ? await LoadImagesAsync(userId, req, ct)
                : Array.Empty<ImageInput>();

            var sb = new StringBuilder();
            if (caps.Streaming)
            {
                await foreach (var chunk in _bedrock.ChatStreamAsync(modelId, effectivePrompt, images, ct))
                {
                    if (chunk.Type == "delta")
                    {
                        sb.Append(chunk.Content);
                        await WriteEvent("delta", new { text = chunk.Content }, ct);
                    }
                }
            }
            else
            {
                var text = await _bedrock.ChatOnceAsync(modelId, effectivePrompt, images, ct);
                sb.Append(text);
                await WriteEvent("delta", new { text }, ct);
            }

            var assistantMessage = new ChatMessage
            {
                SessionId = session.Id,
                Role = MessageRole.Assistant,
                Content = sb.ToString(),
                ModelIdUsed = modelId
            };
            _db.Messages.Add(assistantMessage);
            session.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);

            await WriteEvent("done", new { messageId = assistantMessage.Id, modelId }, ct);
        }
        catch (Exception ex)
        {
            // Headers are already sent, so surface the failure as an SSE event and end cleanly.
            await WriteEvent("error", new { message = ex.Message, modelId }, ct);
        }
    }

    private async Task GenerateImageAsync(
        string userId, ChatSession session, string modelId, string prompt, CancellationToken ct)
    {
        var bytes = await _bedrock.GenerateImageAsync(modelId, prompt, ct);

        var key = _storage.BuildKey(userId, "generated.png");
        using (var ms = new MemoryStream(bytes))
        {
            await _storage.PutObjectAsync(key, ms, "image/png", ct);
        }

        var file = new StoredFile
        {
            UserId = userId,
            FileName = "generated.png",
            ContentType = "image/png",
            SizeBytes = bytes.Length,
            S3Key = key,
            Kind = ModalityKind.Image,
            IsOutput = true
        };

        // Reference the stored image via a presigned URL so <img> works without an auth header.
        var imageUrl = await _storage.PresignGetAsync(key, TimeSpan.FromDays(7));
        var assistantMessage = new ChatMessage
        {
            SessionId = session.Id,
            Role = MessageRole.Assistant,
            Content = $"![generated image]({imageUrl})",
            ModelIdUsed = modelId
        };
        assistantMessage.Attachments.Add(file);

        _db.Messages.Add(assistantMessage);
        session.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        await WriteEvent("delta", new { text = assistantMessage.Content }, ct);
        await WriteEvent("done", new { messageId = assistantMessage.Id, modelId }, ct);
    }

    private async Task<string> BuildPromptAsync(string userId, ChatRequest req, CancellationToken ct)
    {
        if (req.AttachmentIds is not { Length: > 0 }) return req.Prompt;

        var files = await _db.Files
            .Where(f => req.AttachmentIds.Contains(f.Id) && f.UserId == userId)
            .ToListAsync(ct);

        var sb = new StringBuilder(req.Prompt);
        foreach (var file in files.Where(f => f.Kind == ModalityKind.Document))
        {
            await using var stream = await _storage.GetObjectAsync(file.S3Key, ct);
            if (_docParser.CanParse(file.ContentType, file.FileName))
            {
                var text = await _docParser.ExtractTextAsync(stream, file.FileName, ct);
                sb.AppendLine().AppendLine($"--- Attached document: {file.FileName} ---")
                  .AppendLine(text);
            }
        }
        return sb.ToString();
    }

    /// <summary>Loads image attachments from storage to pass to a vision-capable model.</summary>
    private async Task<IReadOnlyList<ImageInput>> LoadImagesAsync(string userId, ChatRequest req, CancellationToken ct)
    {
        if (req.AttachmentIds is not { Length: > 0 }) return Array.Empty<ImageInput>();

        var files = await _db.Files
            .Where(f => req.AttachmentIds.Contains(f.Id) && f.UserId == userId
                        && f.Kind == ModalityKind.Image)
            .ToListAsync(ct);

        var result = new List<ImageInput>();
        foreach (var file in files)
        {
            await using var stream = await _storage.GetObjectAsync(file.S3Key, ct);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct);
            result.Add(new ImageInput(ms.ToArray(), file.ContentType));
        }
        return result;
    }

    private async Task WriteEvent(string type, object data, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(data);
        await Response.WriteAsync($"event: {type}\ndata: {json}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s[..max] + "…";
}
