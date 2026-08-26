using Amazon.Polly;
using Amazon.Polly.Model;

namespace Chatbot.Api.Services;

public interface ISpeechService
{
    Task<(Stream Audio, string ContentType)> SynthesizeAsync(string text, string? voiceId, CancellationToken ct);
}

/// <summary>
/// Text-to-speech via Amazon Polly. Speech-to-text uses Amazon Transcribe, which for streaming
/// runs directly from the browser over WebSocket or via an async job on uploaded audio; that flow
/// is handled in <c>SpeechController</c> using presigned uploads + a Transcribe job.
/// </summary>
public class PollySpeechService : ISpeechService
{
    private readonly IAmazonPolly _polly;

    public PollySpeechService(IAmazonPolly polly) => _polly = polly;

    public async Task<(Stream Audio, string ContentType)> SynthesizeAsync(
        string text, string? voiceId, CancellationToken ct)
    {
        var resp = await _polly.SynthesizeSpeechAsync(new SynthesizeSpeechRequest
        {
            Text = text,
            OutputFormat = OutputFormat.Mp3,
            VoiceId = voiceId ?? VoiceId.Joanna,
            Engine = Engine.Neural
        }, ct);

        return (resp.AudioStream, "audio/mpeg");
    }
}
