using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Product.Business.Interfaces.Market;
using Product.Contracts.Markets;

namespace Product.Api.Controllers;

[ApiController]
[Route("api/v1/terms")]
public class RiskTermsController(IRiskTermsService riskTermsService) : ControllerBase
{
    private readonly IRiskTermsService _riskTermsService = riskTermsService;

    [HttpGet("risk-terms")]
    [AllowAnonymous]
    public async Task<IActionResult> GetRiskTerms(
        [FromQuery] string? version,
        [FromQuery] Guid? marketId,
        CancellationToken ct
    )
    {
        var termVersion = string.IsNullOrWhiteSpace(version) ? null : version.Trim();

        try
        {
            Guid? maybeUserId = null;
            if (TryGetUserId(User, out var uid))
                maybeUserId = uid;

            var result = await _riskTermsService.GetTermsAsync(
                termVersion,
                marketId,
                maybeUserId,
                ct
            );
            var text = result.Text;
            var resolvedVersion = result.TermVersion;
            return Ok(new { termVersion = resolvedVersion, text });
        }
        catch (ArgumentException)
        {
            return NotFound(new { message = "unknown_term_version" });
        }
    }

    [HttpPost("risk-acceptance")]
    [Authorize]
    public async Task<IActionResult> AcceptRiskTerms(
        [FromBody] AcceptMarketRiskTermsRequest request,
        CancellationToken ct
    )
    {
        if (!TryGetUserId(User, out var userId))
            return Unauthorized();

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var ipAddress = HttpContext?.Connection?.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers["User-Agent"].ToString();
        var username =
            User?.Identity?.Name
            ?? User?.FindFirst("name")?.Value
            ?? User?.FindFirst("email")?.Value;
        request.Username = username;

        try
        {
            var result = await _riskTermsService.AcceptAsync(
                userId,
                request,
                ipAddress,
                userAgent,
                ct
            );
            return Ok(result);
        }
        catch (ArgumentException ex) when (ex.Message == "unknown_term_version")
        {
            return BadRequest(new { message = "unknown_term_version" });
        }
        catch (ArgumentException ex) when (ex.Message == "market_missing")
        {
            return BadRequest(new { message = "market_missing" });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "market_not_found" });
        }
    }

    [HttpGet("risk-acceptance")]
    [Authorize]
    public async Task<IActionResult> GetRiskAcceptance(CancellationToken ct)
    {
        if (!TryGetUserId(User, out var userId))
            return Unauthorized();

        var result = await _riskTermsService.GetAcceptanceAsync(userId, null, ct);
        if (result == null)
            return NotFound(new { message = "acceptance_not_found" });

        return Ok(result);
    }

    [HttpGet("risk-acceptance/download")]
    [Authorize]
    public async Task<IActionResult> DownloadRiskAcceptance(CancellationToken ct)
    {
        if (!TryGetUserId(User, out var userId))
            return Unauthorized();

        var username =
            User?.Identity?.Name
            ?? User?.FindFirst("name")?.Value
            ?? User?.FindFirst("email")?.Value;

        try
        {
            var pdf = await _riskTermsService.GetAcceptancePdfAsync(userId, null, username, ct);
            return File(pdf.Content, "application/pdf", pdf.FileName);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "acceptance_not_found" });
        }
    }

    private static bool TryGetUserId(ClaimsPrincipal? user, out Guid userId)
    {
        var userIdStr =
            user?.FindFirst("sub")?.Value
            ?? user?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user?.FindFirst("id")?.Value;

        return Guid.TryParse(userIdStr, out userId);
    }
}
