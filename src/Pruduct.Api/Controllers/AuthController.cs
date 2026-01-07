using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pruduct.Api.Contracts;
using Pruduct.Business.Abstractions;
using Pruduct.Contracts.Auth;

namespace Pruduct.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("signup")]
    [AllowAnonymous]
    public async Task<IActionResult> Signup([FromBody] SignupRequest request, CancellationToken ct)
    {
        var result = await _authService.SignupAsync(request, ct);
        if (!result.Success)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: result.Error);
        }

        return Ok(new ResponseEnvelope<AuthResponse>(result.Data!));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(request, ct);
        if (!result.Success)
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: result.Error);
        }

        return Ok(new ResponseEnvelope<AuthResponse>(result.Data!));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshRequest request,
        CancellationToken ct
    )
    {
        var result = await _authService.RefreshAsync(request, ct);
        if (!result.Success)
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: result.Error);
        }

        return Ok(new ResponseEnvelope<AuthResponse>(result.Data!));
    }
}
