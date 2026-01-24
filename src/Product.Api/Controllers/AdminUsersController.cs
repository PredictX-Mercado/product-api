using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Product.Api.Extensions;
using Product.Business.Interfaces.Users;
using Product.Contracts.Admin;

namespace Product.Api.Controllers;

[ApiController]
[Route("api/v1/admin/users")]
public class AdminUsersController(
    IRolePromotionService rolePromotionService,
    IUserService userService
) : ControllerBase
{
    private readonly IRolePromotionService _rolePromotionService = rolePromotionService;
    private readonly IUserService _userService = userService;

    [Authorize(Policy = "RequireAdminL3")]
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? query,
        [FromQuery] string? by,
        [FromQuery] bool startsWith = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default
    )
    {
        var result = await _userService.GetAllApiAsync(query, by, startsWith, page, pageSize, ct);
        return this.ToActionResult(result);
    }

    [Authorize(Policy = "RequireAdminL3")]
    [HttpPost("{userId:guid}/role/toggle")]
    public async Task<IActionResult> ToggleRole(
        Guid userId,
        [FromBody] PromotionRequest request,
        CancellationToken ct
    )
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Role))
            return BadRequest(new { message = "missing_role" });

        await _rolePromotionService.ToggleRoleAsync(userId, request.Role!, ct);
        return NoContent();
    }
}
