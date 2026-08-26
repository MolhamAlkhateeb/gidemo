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
    private readonly ILogger<ModelsController> _logger;

    public ModelsController(IBedrockService bedrock, ILogger<ModelsController> logger)
    {
        _bedrock = bedrock;
        _logger = logger;
    }

    /// <summary>Lists available Bedrock models plus an "automatic" pseudo-model.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ModelInfo>>> Get(CancellationToken ct)
    {
        var models = await _bedrock.ListModelsAsync(ct);

        var automatic = new ModelInfo(
            Id: "automatic",
            Name: "Automatic",
            Provider: "Router",
            Description: "Automatically picks the best model for your prompt.",
            Capabilities: new ModelCapabilities(true, true, true, true, true, true, true, true),
            Modalities: new[] { "text", "image", "video", "document", "speech" });

        return Ok(new[] { automatic }.Concat(models));
    }
}
