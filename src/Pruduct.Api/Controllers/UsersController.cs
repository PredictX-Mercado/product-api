using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pruduct.Api.Contracts;
using Pruduct.Business.Abstractions;
using Pruduct.Contracts.Auth;
using Pruduct.Contracts.Users;

namespace Pruduct.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMe(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "invalid_token");
        }

        var result = await _userService.GetMeAsync(userId, ct);
        if (!result.Success)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: result.Error);
        }

        return Ok(new ResponseEnvelope<UserView>(result.Data!));
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe(
        [FromBody] UpdateMeRequest request,
        CancellationToken ct
    )
    {
        if (!TryGetUserId(out var userId))
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "invalid_token");
        }

        var result = await _userService.UpdateMeAsync(userId, request, ct);
        if (!result.Success)
        {
            var status =
                result.Error == "username_taken"
                    ? StatusCodes.Status400BadRequest
                    : StatusCodes.Status404NotFound;
            return Problem(statusCode: status, title: result.Error);
        }

        return Ok(new ResponseEnvelope<UserView>(result.Data!));
    }

    private bool TryGetUserId(out Guid userId)
    {
        var sub =
            User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out userId);
    }
}
