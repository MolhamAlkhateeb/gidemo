using Chatbot.Api.Models;

namespace Chatbot.Api.Services;

/// <summary>
/// Static capability map keyed by Bedrock model-id prefix. Bedrock's ListFoundationModels
/// returns input/output modalities, but this map enriches them with UX-relevant flags
/// (streaming, document handling) that drive the adaptive frontend.
/// </summary>
public static class ModelCapabilityMap
{
    // Ordered longest-prefix-first so specific matches win.
    private static readonly (string Prefix, ModelCapabilities Caps, string[] Modalities)[] Map =
    {
        ("anthropic.claude",
            new ModelCapabilities(true, true, true, false, true, false, false, true),
            new[] { "text", "image", "document" }),

        ("amazon.nova-reel",
            new ModelCapabilities(true, true, false, false, false, false, true, false),
            new[] { "text", "image", "video" }),

        ("amazon.nova-canvas",
            new ModelCapabilities(true, true, false, false, false, true, false, false),
            new[] { "text", "image" }),

        ("amazon.nova",
            new ModelCapabilities(true, true, true, false, true, false, false, true),
            new[] { "text", "image", "document" }),

        ("stability.",
            new ModelCapabilities(true, true, false, false, false, true, false, false),
            new[] { "text", "image" }),

        ("amazon.titan-image",
            new ModelCapabilities(true, true, false, false, false, true, false, false),
            new[] { "text", "image" }),

        ("amazon.titan-embed",
            new ModelCapabilities(true, false, false, false, false, false, false, false),
            new[] { "text", "embedding" }),

        ("meta.llama",
            new ModelCapabilities(true, false, false, false, true, false, false, true),
            new[] { "text" }),

        ("mistral.",
            new ModelCapabilities(true, false, false, false, true, false, false, true),
            new[] { "text" }),
    };

    private static readonly ModelCapabilities Default =
        new(true, false, false, false, true, false, false, true);

    public static (ModelCapabilities Caps, string[] Modalities) Resolve(string modelId)
    {
        foreach (var (prefix, caps, modalities) in Map)
        {
            if (modelId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return (caps, modalities);
        }
        return (Default, new[] { "text" });
    }
}
