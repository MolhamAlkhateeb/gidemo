using Chatbot.Api.Models;

namespace Chatbot.Api.Services;

/// <summary>
/// Role-based model access. Roles come from Cognito groups on the user's token:
///   Admin            -> every model
///   TextGeneration   -> text-output models
///   MediaGeneration  -> image/video-output models
/// A user may belong to multiple groups (access is the union).
/// </summary>
public static class ModelAccess
{
    public const string Admin = "Admin";
    public const string TextGeneration = "TextGeneration";
    public const string MediaGeneration = "MediaGeneration";

    public static bool IsTextModel(ModelCapabilities c) => c.TextOutput;
    public static bool IsMediaModel(ModelCapabilities c) => c.ImageOutput || c.VideoOutput;

    public static bool CanUse(IReadOnlyCollection<string> roles, ModelInfo model)
    {
        if (roles.Contains(Admin)) return true;
        if (roles.Contains(TextGeneration) && IsTextModel(model.Capabilities)) return true;
        if (roles.Contains(MediaGeneration) && IsMediaModel(model.Capabilities)) return true;
        return false;
    }

    public static List<ModelInfo> Filter(IReadOnlyCollection<string> roles, IEnumerable<ModelInfo> models)
        => models.Where(m => CanUse(roles, m)).ToList();
}
