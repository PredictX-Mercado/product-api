using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Product.Business.Interfaces.Market;
using Product.Contracts.Markets;
using Product.Data.Database.Contexts;

namespace Product.Api.Controllers;

[ApiController]
[Route("api/v1/markets")]
public class MarketsController(IMarketService svc, AppDbContext db) : ControllerBase
{
    private readonly IMarketService _svc = svc;
    private readonly AppDbContext _db = db;

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Explore(
        [FromQuery] ExploreFilterRequest req,
        CancellationToken ct
    )
    {
        var (items, total, page, pageSize) = await _svc.ExploreMarketsAsync(req, ct);
        return Ok(
            new
            {
                items,
                total,
                page,
                pageSize,
            }
        );
    }

    [HttpGet("search")]
    [AllowAnonymous]
    public async Task<IActionResult> SearchByTitle(
        [FromQuery(Name = "title")] string? title,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default
    )
    {
        if (string.IsNullOrWhiteSpace(title))
            return BadRequest(new { message = "missing_title" });

        var req = new ExploreFilterRequest
        {
            Search = title,
            Page = page <= 0 ? 1 : page,
            PageSize = pageSize <= 0 ? 20 : pageSize,
        };

        var (items, total, curPage, curPageSize) = await _svc.ExploreMarketsAsync(req, ct);
        return Ok(
            new
            {
                items,
                total,
                page = curPage,
                pageSize = curPageSize,
            }
        );
    }

    [Authorize(Policy = "RequireAdminL2")]
    [HttpPost("create-market")]
    public async Task<IActionResult> CreateAdmin(
        [FromBody] CreateMarketRequest req,
        CancellationToken ct
    )
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var idem = Request.Headers["Idempotency-Key"].FirstOrDefault();
        var confirm = Request.Headers["X-Confirm-Low-Liquidity"].FirstOrDefault();
        var confirmLowLiquidity = string.Equals(
            confirm,
            "true",
            StringComparison.OrdinalIgnoreCase
        );

        Guid? userId = null;
        var userIdStr = User?.FindFirst("sub")?.Value;
        if (Guid.TryParse(userIdStr, out var g))
            userId = g;
        var userEmail = User?.FindFirst("email")?.Value;

        var isAdminL2_L3 = HasAnyRole(User, "admin_l2", "admin_l3");

        var created = await _svc.CreateMarketAsync(
            req,
            userId,
            userEmail,
            isAdminL2_L3,
            idem,
            confirmLowLiquidity,
            ct
        );
        return CreatedAtAction(nameof(Get), new { marketId = created.Id }, created);
    }

    [Authorize(Policy = "RequireAdminL2")]
    [HttpPut("{marketId}")]
    public async Task<IActionResult> UpdateAdmin(
        Guid marketId,
        [FromBody] UpdateMarketRequest req,
        CancellationToken ct
    )
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        Guid? userId = null;
        var userIdStr = User?.FindFirst("sub")?.Value;
        if (Guid.TryParse(userIdStr, out var g))
            userId = g;

        var isAdminL2_L3 = HasAnyRole(User, "admin_l2", "admin_l3");

        var updated = await _svc.UpdateMarketAsync(marketId, req, userId, isAdminL2_L3, ct);
        return Ok(updated);
    }

    [Authorize(Policy = "RequireAdminL2")]
    [HttpDelete("{marketId}")]
    public async Task<IActionResult> DeleteAdmin(Guid marketId, CancellationToken ct)
    {
        Guid? userId = null;
        var userIdStr = User?.FindFirst("sub")?.Value;
        if (Guid.TryParse(userIdStr, out var g))
            userId = g;

        var existing = await _svc.GetMarketAsync(marketId, null, ct);
        if (existing == null)
            return NotFound();

        await _svc.DeleteMarketAsync(marketId, userId, ct);
        return NoContent();
    }

    [HttpGet("{marketId}")]
    [AllowAnonymous]
    public async Task<IActionResult> Get(Guid marketId, CancellationToken ct)
    {
        var userId = User?.FindFirst("sub")?.Value;
        Guid? uid = null;
        if (Guid.TryParse(userId, out var g))
            uid = g;
        var m = await _svc.GetMarketAsync(marketId, uid, ct);
        if (m == null)
            return NotFound();
        return Ok(m);
    }

    [HttpGet("{marketId}/history")]
    [AllowAnonymous]
    public async Task<IActionResult> History(
        Guid marketId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? resolution,
        CancellationToken ct
    )
    {
        var points = await _svc.GetMarketHistoryAsync(marketId, from, to, resolution, ct);
        return Ok(points);
    }

    [HttpGet("market-categories")]
    [AllowAnonymous]
    public async Task<IActionResult> Categories(CancellationToken ct)
    {
        var cats = await _db
            .Categories.OrderBy(c => c.Name)
            .Select(c => new
            {
                id = c.Id,
                name = c.Name,
                slug = c.Slug,
            })
            .ToListAsync(ct);
        return Ok(cats);
    }

    [HttpGet("{marketId}/market-categories")]
    [AllowAnonymous]
    public async Task<IActionResult> MarketCategories(Guid marketId, CancellationToken ct)
    {
        var cats = await _db
            .MarketCategories.Where(mc => mc.MarketId == marketId && mc.Category != null)
            .Include(mc => mc.Category)
            .Select(mc => new
            {
                id = mc.Category!.Id,
                name = mc.Category!.Name,
                slug = mc.Category!.Slug,
            })
            .ToListAsync(ct);
        return Ok(cats);
    }

    [HttpPost("trade")]
    [Authorize]
    public async Task<IActionResult> Buy([FromBody] BuyRequest req, CancellationToken ct)
    {
        var userIdStr =
            User?.FindFirst("sub")?.Value
            ?? User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User?.FindFirst("id")?.Value;

        if (string.IsNullOrWhiteSpace(userIdStr) || !Guid.TryParse(userIdStr, out var uid))
            return Unauthorized();

        if (req == null || req.MarketId == Guid.Empty)
            return BadRequest("market_missing");

        var marketId = req.MarketId;

        var res = await _svc.BuyAsync(marketId, uid, req.Side, req.Amount, req.IdempotencyKey, ct);
        return Ok(res);
    }

    private static bool HasAnyRole(ClaimsPrincipal? user, params string[] roles)
    {
        if (user == null || roles.Length == 0)
            return false;

        return roles.Any(r => user.IsInRole(r) || user.IsInRole(r.ToUpperInvariant()));
    }
}
