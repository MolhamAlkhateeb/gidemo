using Chatbot.Api.Models;
using Chatbot.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chatbot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SpeechController : ControllerBase
{
    private readonly ISpeechService _speech;

    public SpeechController(ISpeechService speech) => _speech = speech;

    /// <summary>Text-to-speech: returns an MP3 stream for the given text (Amazon Polly).</summary>
    [HttpPost("tts")]
    public async Task<IActionResult> TextToSpeech(TtsRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Text)) return BadRequest("Text is required.");
        var (audio, contentType) = await _speech.SynthesizeAsync(req.Text, req.VoiceId, ct);
        return File(audio, contentType);
    }

    // NOTE: Speech-to-text (Amazon Transcribe) for uploaded audio is triggered from the
    // frontend via /api/files/presign to upload the clip, then a Transcribe job reads it.
    // Real-time mic transcription can also use the browser Web Speech API as a fallback.
}
