using Chatbot.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chatbot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MeController : ControllerBase
{
    private readonly IConfiguration _config;

    public MeController(IConfiguration config) => _config = config;

    /// <summary>Returns the caller's id and access roles (Cognito groups).</summary>
    [HttpGet]
    public ActionResult<object> Get()
    {
        var roles = HttpContext.GetRoles(_config);
        return Ok(new
        {
            userId = HttpContext.GetUserId(),
            roles
        });
    }
}
