using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chatbot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class ConfigController : ControllerBase
{
    private readonly IConfiguration _config;

    public ConfigController(IConfiguration config) => _config = config;

    /// <summary>Public runtime config the SPA needs before login (no secrets).</summary>
    [HttpGet]
    public ActionResult<object> Get()
    {
        var disableAuth = _config.GetValue<bool>("DisableAuth");
        return Ok(new
        {
            authEnabled = !disableAuth,
            region = _config["Cognito:Region"],
            userPoolClientId = _config["Cognito:Audience"]
        });
    }
}
