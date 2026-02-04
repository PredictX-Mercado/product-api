using Product.Contracts.Markets;

namespace Product.Business.Interfaces.Market;

public interface IRiskTermsService
{
    Task<(string TermVersion, string Text)> GetTermsAsync(
        string? version,
        Guid? marketId,
        Guid? userId = null,
        CancellationToken ct = default
    );

    Task<RiskTermsResponse> AcceptAsync(
        Guid userId,
        AcceptMarketRiskTermsRequest request,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken ct = default
    );

    Task<RiskTermsResponse?> GetAcceptanceAsync(
        Guid userId,
        Guid? marketId,
        CancellationToken ct = default
    );

    Task<(RiskTermsResponse Acceptance, string Text)?> GetAcceptanceWithTextAsync(
        Guid userId,
        Guid? marketId,
        CancellationToken ct = default
    );

    Task<(string FileName, byte[] Content)> GetAcceptancePdfAsync(
        Guid userId,
        Guid? marketId,
        string? username,
        CancellationToken ct = default
    );
}
