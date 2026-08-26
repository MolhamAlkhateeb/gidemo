using Chatbot.Api.Domain;

namespace Chatbot.Api.Models;

/// <summary>Capability descriptor surfaced to the frontend so the UI can adapt per model.</summary>
public record ModelCapabilities(
    bool TextInput,
    bool ImageInput,
    bool DocumentInput,
    bool AudioInput,
    bool TextOutput,
    bool ImageOutput,
    bool VideoOutput,
    bool Streaming);

public record ModelInfo(
    string Id,
    string Name,
    string Provider,
    string Description,
    ModelCapabilities Capabilities,
    string[] Modalities);

/// <summary>Request to start/continue a chat turn.</summary>
public record ChatRequest(
    Guid? SessionId,
    string ModelId,          // may be "automatic"
    string Prompt,
    Guid[]? AttachmentIds);

public record ChatChunk(string Type, string Content, string? ModelId = null);

public record CreateSessionRequest(string? Title, string ModelId, bool AutomaticModel);

public record PresignUploadRequest(string FileName, string ContentType, long SizeBytes);

public record PresignUploadResponse(Guid FileId, string UploadUrl, string S3Key);

public record TtsRequest(string Text, string? VoiceId);

/// <summary>An image supplied to a vision-capable model, read from storage.</summary>
public record ImageInput(byte[] Bytes, string ContentType);
