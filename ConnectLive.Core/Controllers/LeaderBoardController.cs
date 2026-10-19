using Microsoft.AspNetCore.Mvc;

namespace ConnectLive.Core.Controllers;
[ApiController]
[Microsoft.AspNetCore.Authorization.Authorize]
[Route("[controller]")]
public class LeaderBoardController : ControllerBase
{
    [HttpGet]
    [Route("get-session-score")]
    public async Task<IActionResult> GetSessionScore()
    {
        return Ok("GetSessionScore");
    }
}
