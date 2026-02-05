using System.Security.Claims;
using Product.Business.Interfaces.Results;
using Product.Contracts.Wallet;

namespace Product.Business.Interfaces.Wallet;

public interface IReceiptService
{
    Task<ApiResult> GetReceiptsApiAsync(
        ClaimsPrincipal principal,
        string? cursor,
        int? limit,
        CancellationToken ct = default
    );

    Task<ApiResult> GetReceiptApiAsync(
        ClaimsPrincipal principal,
        Guid receiptId,
        CancellationToken ct = default
    );

    Task<ServiceResult<ReceiptListResponse>> GetReceiptsAsync(
        Guid userId,
        string? cursor,
        int? limit,
        CancellationToken ct = default
    );

    Task<ServiceResult<ReceiptItem>> GetReceiptAsync(
        Guid userId,
        Guid receiptId,
        CancellationToken ct = default
    );

    Task<bool> EnsureDepositReceiptAsync(Guid paymentIntentId, CancellationToken ct = default);

    Task<int> BackfillDepositReceiptsAsync(int take = 100, CancellationToken ct = default);
    Task<int> BackfillBuyReceiptsAsync(int take = 100, CancellationToken ct = default);
}
