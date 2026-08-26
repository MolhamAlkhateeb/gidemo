using Amazon.Bedrock;
using Amazon.Bedrock.Model;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Chatbot.Api.Models;
using System.Text;
using System.Text.Json;

namespace Chatbot.Api.Services;

public interface IBedrockService
{
    Task<IReadOnlyList<ModelInfo>> ListModelsAsync(CancellationToken ct);
    IAsyncEnumerable<ChatChunk> ChatStreamAsync(string modelId, string prompt, IReadOnlyList<ImageInput> images, CancellationToken ct);
    Task<string> ChatOnceAsync(string modelId, string prompt, IReadOnlyList<ImageInput> images, CancellationToken ct);
    Task<string> RouteAsync(string prompt, IReadOnlyList<ModelInfo> models, bool hasImageAttachment, CancellationToken ct);
    Task<byte[]> GenerateImageAsync(string modelId, string prompt, CancellationToken ct);
}

public class BedrockService : IBedrockService
{
    private readonly IAmazonBedrock _control;
    private readonly IAmazonBedrockRuntime _runtime;
    private readonly ILogger<BedrockService> _logger;
    private readonly string _routerModelId;

    public BedrockService(
        IAmazonBedrock control,
        IAmazonBedrockRuntime runtime,
        IConfiguration config,
        ILogger<BedrockService> logger)
    {
        _control = control;
        _runtime = runtime;
        _logger = logger;
        _routerModelId = config["Bedrock:RouterModelId"]
            ?? "amazon.nova-micro-v1:0";
    }

    public async Task<IReadOnlyList<ModelInfo>> ListModelsAsync(CancellationToken ct)
    {
        var resp = await _control.ListFoundationModelsAsync(
            new ListFoundationModelsRequest { ByOutputModality = null }, ct);

        var result = new List<ModelInfo>();
        foreach (var m in resp.ModelSummaries)
        {
            // Only surface models usable for on-demand inference.
            var onDemand = m.InferenceTypesSupported?
                .Any(t => t == InferenceType.ON_DEMAND) ?? false;
            if (!onDemand) continue;

            // Skip legacy models — they cannot be invoked without prior recent usage.
            if (m.ModelLifecycle?.Status == FoundationModelLifecycleStatus.LEGACY) continue;

            // Derive capabilities from what Bedrock actually reports, not guessed prefixes.
            var inputs = (m.InputModalities ?? new()).Select(x => x?.ToString()?.ToUpperInvariant()).ToHashSet();
            var outputs = (m.OutputModalities ?? new()).Select(x => x?.ToString()?.ToUpperInvariant()).ToHashSet();

            var textOut = outputs.Contains("TEXT");
            var imageOut = outputs.Contains("IMAGE");
            var videoOut = outputs.Contains("VIDEO");

            // Embedding-only models can't chat or generate media — hide them from the picker.
            if (!textOut && !imageOut && !videoOut) continue;

            var textIn = inputs.Contains("TEXT");
            var caps = new ModelCapabilities(
                TextInput: textIn,
                ImageInput: inputs.Contains("IMAGE"),
                DocumentInput: textIn,                    // docs are injected as extracted text
                AudioInput: inputs.Contains("AUDIO"),
                TextOutput: textOut,
                ImageOutput: imageOut,
                VideoOutput: videoOut,
                Streaming: m.ResponseStreamingSupported ?? false);

            var modalities = inputs.Concat(outputs)
                .Where(x => x is not null)
                .Select(x => x!.ToLowerInvariant())
                .Distinct()
                .ToArray();

            result.Add(new ModelInfo(
                Id: m.ModelId,
                Name: m.ModelName ?? m.ModelId,
                Provider: m.ProviderName ?? "Unknown",
                Description: BuildDescription(m),
                Capabilities: caps,
                Modalities: modalities));
        }
        return result;
    }

    private static string BuildDescription(FoundationModelSummary m)
    {
        var inputs = m.InputModalities is { Count: > 0 } ? string.Join(", ", m.InputModalities) : "text";
        var outputs = m.OutputModalities is { Count: > 0 } ? string.Join(", ", m.OutputModalities) : "text";
        return $"Inputs: {inputs}. Outputs: {outputs}.";
    }

    public async IAsyncEnumerable<ChatChunk> ChatStreamAsync(
        string modelId, string prompt, IReadOnlyList<ImageInput> images,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var request = new ConverseStreamRequest
        {
            ModelId = modelId,
            Messages = new List<Message>
            {
                new()
                {
                    Role = ConversationRole.User,
                    Content = BuildUserContent(prompt, images)
                }
            }
        };

        var response = await _runtime.ConverseStreamAsync(request, ct);

        yield return new ChatChunk("start", string.Empty, modelId);

        await foreach (var evt in response.Stream.WithCancellation(ct))
        {
            if (evt is ContentBlockDeltaEvent delta && delta.Delta?.Text is { Length: > 0 } text)
            {
                yield return new ChatChunk("delta", text, modelId);
            }
        }

        yield return new ChatChunk("end", string.Empty, modelId);
    }

    /// <summary>Non-streaming chat for models that don't support ConverseStream.</summary>
    public async Task<string> ChatOnceAsync(string modelId, string prompt, IReadOnlyList<ImageInput> images, CancellationToken ct)
    {
        var response = await _runtime.ConverseAsync(new ConverseRequest
        {
            ModelId = modelId,
            Messages = new List<Message>
            {
                new()
                {
                    Role = ConversationRole.User,
                    Content = BuildUserContent(prompt, images)
                }
            }
        }, ct);

        return response.Output?.Message?.Content?.FirstOrDefault()?.Text ?? string.Empty;
    }

    // Builds Converse content blocks: any images first, then the text prompt.
    private static List<ContentBlock> BuildUserContent(string prompt, IReadOnlyList<ImageInput> images)
    {
        var content = new List<ContentBlock>();
        foreach (var img in images)
        {
            content.Add(new ContentBlock
            {
                Image = new ImageBlock
                {
                    Format = MapImageFormat(img.ContentType),
                    Source = new ImageSource { Bytes = new MemoryStream(img.Bytes) }
                }
            });
        }
        content.Add(new ContentBlock { Text = prompt });
        return content;
    }

    private static ImageFormat MapImageFormat(string contentType) => contentType.ToLowerInvariant() switch
    {
        "image/jpeg" or "image/jpg" => ImageFormat.Jpeg,
        "image/gif" => ImageFormat.Gif,
        "image/webp" => ImageFormat.Webp,
        _ => ImageFormat.Png
    };

    /// <summary>
    /// "Automatic" model selection. Uses fast keyword heuristics for obvious image/video
    /// generation or image-analysis intent, then falls back to a cheap model classifier.
    /// </summary>
    public async Task<string> RouteAsync(
        string prompt, IReadOnlyList<ModelInfo> models, bool hasImageAttachment, CancellationToken ct)
    {
        // 1. An attached image implies analysis -> prefer a vision (image-in + text-out) model.
        if (hasImageAttachment)
        {
            var vision = FirstWith(models, c => c.ImageInput && c.TextOutput);
            if (vision is not null) return vision;
        }

        var lower = prompt.ToLowerInvariant();

        // 2. Explicit video-generation intent -> video-output model.
        if (MatchesGeneration(lower, "video", "animation", "animate", "clip", "movie"))
        {
            var video = FirstWith(models, c => c.VideoOutput);
            if (video is not null) return video;
        }

        // 3. Explicit image-generation intent -> image-output model.
        if (MatchesGeneration(lower, "image", "picture", "photo", "logo", "art",
                "drawing", "illustration", "render", "painting", "poster"))
        {
            var image = FirstWith(models, c => c.ImageOutput && !c.TextOutput);
            if (image is not null) return image;
        }

        // 4. Otherwise ask the cheap classifier model.
        var catalog = string.Join("\n", models.Select(m =>
            $"- {m.Id} | in:{Inputs(m)} out:{Outputs(m)} | {m.Provider}"));

        var routerPrompt =
            "You are a model router. Choose the single best model for the USER PROMPT.\n" +
            "Rules: for creating/generating an image pick an image-output model; " +
            "for video pick a video-output model; otherwise pick a text model.\n" +
            "Reply with ONLY the exact model id and nothing else.\n\n" +
            $"AVAILABLE MODELS:\n{catalog}\n\nUSER PROMPT:\n{prompt}";

        try
        {
            var request = new ConverseRequest
            {
                ModelId = _routerModelId,
                Messages = new List<Message>
                {
                    new()
                    {
                        Role = ConversationRole.User,
                        Content = new List<ContentBlock> { new() { Text = routerPrompt } }
                    }
                },
                InferenceConfig = new InferenceConfiguration { MaxTokens = 40, Temperature = 0f }
            };

            var resp = await _runtime.ConverseAsync(request, ct);
            var answer = resp.Output?.Message?.Content?.FirstOrDefault()?.Text?.Trim();

            var match = models.FirstOrDefault(m => answer is not null && answer.Contains(m.Id));
            if (match is not null) return match.Id;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Automatic routing failed; falling back to default text model.");
        }

        // Fallback: first text-capable streaming model.
        return models.FirstOrDefault(m => m.Capabilities is { TextOutput: true, Streaming: true })?.Id
            ?? models.First().Id;
    }

    private static string? FirstWith(IReadOnlyList<ModelInfo> models, Func<ModelCapabilities, bool> pred)
        => models.FirstOrDefault(m => pred(m.Capabilities))?.Id;

    // True when the text expresses intent to create something of the given kinds.
    private static bool MatchesGeneration(string lower, params string[] kinds)
    {
        string[] verbs = { "generate", "create", "make", "draw", "render", "produce", "design", "paint" };
        var mentionsKind = kinds.Any(lower.Contains);
        var mentionsVerb = verbs.Any(lower.Contains);
        return mentionsKind && mentionsVerb;
    }

    private static string Inputs(ModelInfo m)
    {
        var c = m.Capabilities;
        var parts = new List<string>();
        if (c.TextInput) parts.Add("text");
        if (c.ImageInput) parts.Add("image");
        if (c.AudioInput) parts.Add("audio");
        return string.Join("/", parts);
    }

    private static string Outputs(ModelInfo m)
    {
        var c = m.Capabilities;
        var parts = new List<string>();
        if (c.TextOutput) parts.Add("text");
        if (c.ImageOutput) parts.Add("image");
        if (c.VideoOutput) parts.Add("video");
        return string.Join("/", parts);
    }

    /// <summary>
    /// Generates an image via the InvokeModel API (image models do not support streaming).
    /// Supports the Amazon Nova Canvas / Titan schema and Stability AI models.
    /// Returns raw PNG bytes.
    /// </summary>
    public async Task<byte[]> GenerateImageAsync(string modelId, string prompt, CancellationToken ct)
    {
        var body = BuildImageRequestBody(modelId, prompt);

        var response = await _runtime.InvokeModelAsync(new InvokeModelRequest
        {
            ModelId = modelId,
            ContentType = "application/json",
            Accept = "application/json",
            Body = new MemoryStream(Encoding.UTF8.GetBytes(body))
        }, ct);

        using var doc = await JsonDocument.ParseAsync(response.Body, cancellationToken: ct);
        var root = doc.RootElement;

        // Amazon (Nova Canvas / Titan): { "images": ["<base64>"] }
        if (root.TryGetProperty("images", out var images) && images.GetArrayLength() > 0)
            return Convert.FromBase64String(images[0].GetString()!);

        // Stability AI: { "artifacts": [ { "base64": "..." } ] } or { "images": [...] }
        if (root.TryGetProperty("artifacts", out var artifacts) && artifacts.GetArrayLength() > 0)
            return Convert.FromBase64String(artifacts[0].GetProperty("base64").GetString()!);

        throw new InvalidOperationException($"Model {modelId} returned no image data.");
    }

    private static string BuildImageRequestBody(string modelId, string prompt)
    {
        // Stability AI models use a different request schema.
        if (modelId.StartsWith("stability.", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(new
            {
                prompt,
                mode = "text-to-image",
                aspect_ratio = "1:1",
                output_format = "png"
            });
        }

        // Amazon Nova Canvas and Titan Image share this schema.
        return JsonSerializer.Serialize(new
        {
            taskType = "TEXT_IMAGE",
            textToImageParams = new { text = prompt },
            imageGenerationConfig = new
            {
                numberOfImages = 1,
                height = 1024,
                width = 1024,
                cfgScale = 8.0,
                quality = "standard"
            }
        });
    }
}
