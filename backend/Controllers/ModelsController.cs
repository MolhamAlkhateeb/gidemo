using Chatbot.Api.Infrastructure;
using Chatbot.Api.Models;
using Chatbot.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chatbot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ModelsController : ControllerBase
{
    private readonly IBedrockService _bedrock;
    private readonly IConfiguration _config;
    private readonly ILogger<ModelsController> _logger;

    public ModelsController(IBedrockService bedrock, IConfiguration config, ILogger<ModelsController> logger)
    {
        _bedrock = bedrock;
        _config = config;
        _logger = logger;
    }

    /// <summary>Lists the Bedrock models the caller's role allows, plus "automatic".</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ModelInfo>>> Get(CancellationToken ct)
    {
        var models = await _bedrock.ListModelsAsync(ct);
        var roles = HttpContext.GetRoles(_config);
        var allowed = ModelAccess.Filter(roles, models);

        var result = new List<ModelInfo>();

        // Offer "automatic" only when the user has at least one usable model.
        if (allowed.Count > 0)
        {
            result.Add(new ModelInfo(
                Id: "automatic",
                Name: "Automatic",
                Provider: "Router",
                Description: "Automatically picks the best model for your prompt.",
                Capabilities: new ModelCapabilities(true, true, true, true, true, true, true, true),
                Modalities: new[] { "text", "image", "video", "document", "speech" }));
        }

        result.AddRange(allowed);
        return Ok(result);
    }
}
