using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Product.Business.Interfaces.Results;
using Product.Business.Interfaces.Wallet;
using Product.Contracts.Wallet;
using Product.Data.Interfaces.Repositories;
using Product.Data.Models.Markets;
using Product.Data.Models.Wallet;

namespace Product.Business.Services.Wallet;

public partial class ReceiptService : IReceiptService
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 200;

    private readonly IWalletRepository _walletRepository;

    public ReceiptService(IWalletRepository walletRepository)
    {
        _walletRepository = walletRepository;
    }

    public async Task<ApiResult> GetReceiptsApiAsync(
        ClaimsPrincipal principal,
        string? cursor,
        int? limit,
        CancellationToken ct = default
    )
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return ApiResult.Problem(StatusCodes.Status401Unauthorized, "invalid_token");
        }

        var result = await GetReceiptsAsync(userId, cursor, limit, ct);
        if (!result.Success)
        {
            var status =
                result.Error == "invalid_cursor"
                    ? StatusCodes.Status400BadRequest
                    : StatusCodes.Status404NotFound;
            return ApiResult.Problem(status, result.Error ?? "unknown");
        }

        return ApiResult.Ok(result.Data, envelope: true);
    }

    public async Task<ApiResult> GetReceiptApiAsync(
        ClaimsPrincipal principal,
        Guid receiptId,
        CancellationToken ct = default
    )
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return ApiResult.Problem(StatusCodes.Status401Unauthorized, "invalid_token");
        }

        var result = await GetReceiptAsync(userId, receiptId, ct);
        if (!result.Success)
        {
            return ApiResult.Problem(StatusCodes.Status404NotFound, result.Error ?? "not_found");
        }

        return ApiResult.Ok(result.Data, envelope: true);
    }

    public async Task<ServiceResult<ReceiptListResponse>> GetReceiptsAsync(
        Guid userId,
        string? cursor,
        int? limit,
        CancellationToken ct = default
    )
    {
        var pageSize = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        if (!TryParseCursor(cursor, out var cursorTime))
        {
            return ServiceResult<ReceiptListResponse>.Fail("invalid_cursor");
        }

        var receipts = await _walletRepository.GetReceiptsAsync(
            userId,
            cursorTime,
            pageSize + 1,
            ct
        );

        var hasMore = receipts.Count > pageSize;
        var page = receipts.Take(pageSize).ToList();
        var nextCursor = hasMore && page.Count > 0 ? page.Last().CreatedAt.ToString("o") : null;

        var paymentLookup = await LoadPaymentIntentLookupAsync(page, ct);
        var marketLookup = await LoadMarketLookupAsync(page, ct);

        var items = page.Select(r => MapReceiptItem(r, paymentLookup, marketLookup)).ToList();

        return ServiceResult<ReceiptListResponse>.Ok(
            new ReceiptListResponse { Items = items, NextCursor = nextCursor }
        );
    }

    public async Task<ServiceResult<ReceiptItem>> GetReceiptAsync(
        Guid userId,
        Guid receiptId,
        CancellationToken ct = default
    )
    {
        var r = await _walletRepository.GetReceiptByIdAsync(receiptId, ct);
        if (r is null)
            return ServiceResult<ReceiptItem>.Fail("not_found");
        if (r.UserId != userId)
            return ServiceResult<ReceiptItem>.Fail("not_found");

        var paymentLookup = await LoadPaymentIntentLookupAsync(new[] { r }, ct);
        var marketLookup = await LoadMarketLookupAsync(new[] { r }, ct);

        var item = MapReceiptItem(r, paymentLookup, marketLookup);

        return ServiceResult<ReceiptItem>.Ok(item);
    }

    public async Task<int> BackfillDepositReceiptsAsync(
        int take = 100,
        CancellationToken ct = default
    )
    {
        var intents = await _walletRepository.GetApprovedPaymentIntentsWithoutReceiptAsync(
            Math.Clamp(take, 1, 500),
            ct
        );
        var created = 0;
        foreach (var intent in intents)
        {
            var ledger = await _walletRepository.GetLedgerEntryByReferenceAsync(
                "PaymentIntent",
                intent.Id,
                ct
            );
            var targetReferenceId = ledger?.Id ?? intent.Id;
            if (await _walletRepository.ReceiptExistsForReferenceAsync(targetReferenceId, ct))
            {
                continue;
            }

            var receipt = new Receipt
            {
                Id = Guid.NewGuid(),
                UserId = intent.UserId,
                Type = "deposit",
                Amount = intent.Amount,
                Currency = intent.Currency,
                Provider = intent.Provider,
                PaymentIntentId = intent.Id,
                PaymentMethod = intent.PaymentMethod,
                PaymentExpiresAt = intent.ExpiresAt,
                CheckoutUrl = intent.CheckoutUrl,
                ExternalPaymentId = intent.ExternalPaymentId,
                Description = $"Depósito via {intent.Provider}",
                ProviderPaymentId = long.TryParse(intent.ExternalPaymentId, out var pid)
                    ? pid
                    : null,
                ProviderPaymentIdText = intent.ExternalPaymentId,
                ReferenceType = ledger is not null ? "LedgerEntry" : "PaymentIntent",
                ReferenceId = targetReferenceId,
                PayloadJson = JsonSerializer.Serialize(new { intent, note = "backfill" }),
            };
            await _walletRepository.AddReceiptAsync(receipt, ct);
            created++;
        }
        return created;
    }

    public async Task<int> BackfillBuyReceiptsAsync(int take = 100, CancellationToken ct = default)
    {
        var txs = await _walletRepository.GetBuyTransactionsWithoutReceiptAsync(
            Math.Clamp(take, 1, 500),
            ct
        );
        if (txs.Count == 0)
            return 0;

        var marketLookup = await LoadMarketLookupAsync(txs, ct);
        var created = 0;
        foreach (var tx in txs)
        {
            var amount = -tx.NetAmount; // mirror live receipt (ledger negative)
            var ledger = await _walletRepository.FindLedgerEntryForBuyAsync(
                tx.UserId,
                tx.MarketId ?? Guid.Empty,
                amount,
                tx.CreatedAt,
                ct
            );
            var targetReferenceId = ledger?.Id ?? tx.Id;
            if (await _walletRepository.ReceiptExistsForReferenceAsync(targetReferenceId, ct))
            {
                continue;
            }

            var localizedDescription = LocalizeYesNo(tx.Description ?? string.Empty);
            var contracts =
                TryExtractContracts(localizedDescription)
                ?? TryExtractContracts(tx.Description ?? string.Empty);
            decimal? unitPrice = null;
            if (contracts.HasValue && contracts.Value > 0)
            {
                unitPrice = Math.Round(
                    Math.Abs(amount) / contracts.Value,
                    2,
                    MidpointRounding.AwayFromZero
                );
            }
            var receipt = new Receipt
            {
                Id = Guid.NewGuid(),
                UserId = tx.UserId,
                Type = "buy",
                Amount = amount,
                Currency = "BRL",
                Provider = "INTERNAL",
                MarketId = tx.MarketId,
                MarketTitleSnapshot =
                    tx.MarketId.HasValue
                    && marketLookup.TryGetValue(tx.MarketId.Value, out var market)
                        ? market.Title
                        : null,
                MarketSlugSnapshot = null,
                Description = localizedDescription,
                ReferenceType = ledger is not null ? "LedgerEntry" : "MarketTransaction",
                ReferenceId = targetReferenceId,
                PayloadJson = JsonSerializer.Serialize(
                    new
                    {
                        tx = new
                        {
                            tx.Id,
                            tx.UserId,
                            tx.Type,
                            tx.Amount,
                            tx.NetAmount,
                            tx.MarketId,
                            Description = localizedDescription,
                            tx.CreatedAt,
                            tx.UpdatedAt,
                            contracts,
                            unitPrice,
                        },
                        note = "backfill",
                    }
                ),
            };
            await _walletRepository.AddReceiptAsync(receipt, ct);
            created++;
        }
        return created;
    }

    private static bool TryParseCursor(string? cursor, out DateTimeOffset? cursorTime)
    {
        cursorTime = null;
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return true;
        }

        if (!DateTimeOffset.TryParse(cursor, out var parsed))
        {
            return false;
        }

        cursorTime = parsed;
        return true;
    }

    private static string LocalizeYesNo(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return description;

        return description
            .Replace(" contratos yes", " contratos sim")
            .Replace(" contratos no", " contratos não")
            .Replace(" yes", " sim")
            .Replace(" no", " não");
    }

    private static int? TryExtractContracts(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return null;

        var match = MyRegex().Match(description);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var value))
        {
            return value;
        }

        return null;
    }

    private async Task<Dictionary<Guid, PaymentIntent>> LoadPaymentIntentLookupAsync(
        IEnumerable<Receipt> receipts,
        CancellationToken ct
    )
    {
        var ids = receipts
            .Where(r => r.PaymentIntentId.HasValue)
            .Select(r => r.PaymentIntentId!.Value)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            return new Dictionary<Guid, PaymentIntent>();

        var intents = await _walletRepository.GetPaymentIntentsByIdsAsync(ids, ct);
        return intents.ToDictionary(i => i.Id, i => i);
    }

    private async Task<Dictionary<Guid, Market>> LoadMarketLookupAsync(
        IEnumerable<Receipt> receipts,
        CancellationToken ct
    )
    {
        var ids = receipts
            .Where(r => r.MarketId.HasValue)
            .Select(r => r.MarketId!.Value)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            return new Dictionary<Guid, Market>();

        var markets = await _walletRepository.GetMarketsByIdsAsync(ids, ct);
        return markets.ToDictionary(m => m.Id, m => m);
    }

    private async Task<Dictionary<Guid, Market>> LoadMarketLookupAsync(
        IEnumerable<Transaction> txs,
        CancellationToken ct
    )
    {
        var ids = txs.Where(t => t.MarketId.HasValue)
            .Select(t => t.MarketId!.Value)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            return new Dictionary<Guid, Market>();

        var markets = await _walletRepository.GetMarketsByIdsAsync(ids, ct);
        return markets.ToDictionary(m => m.Id, m => m);
    }

    private static ReceiptItem MapReceiptItem(
        Receipt r,
        IDictionary<Guid, PaymentIntent> paymentLookup,
        IDictionary<Guid, Market> marketLookup
    )
    {
        var description = r.Description ?? TryExtractDescription(r.PayloadJson);

        var contracts = TryGetContracts(r, description);
        decimal? unitPrice = null;
        if (contracts.HasValue && contracts.Value > 0)
        {
            unitPrice = Math.Round(
                Math.Abs(r.Amount) / contracts.Value,
                2,
                MidpointRounding.AwayFromZero
            );
        }

        var market = BuildMarket(r, marketLookup);
        var payment = BuildPayment(r, paymentLookup);

        return new ReceiptItem
        {
            Id = r.Id,
            Type = r.Type,
            Amount = r.Amount,
            Currency = r.Currency,
            Provider = r.Provider,
            CreatedAt = r.CreatedAt,
            Description = description,
            Contracts = contracts,
            UnitPrice = unitPrice,
            Market = market,
            Payment = payment,
        };
    }

    private static ReceiptMarket? BuildMarket(
        Receipt receipt,
        IDictionary<Guid, Market> marketLookup
    )
    {
        if (!receipt.MarketId.HasValue)
            return null;

        var marketId = receipt.MarketId.Value;
        marketLookup.TryGetValue(marketId, out var market);

        var title = receipt.MarketTitleSnapshot ?? market?.Title;
        if (string.IsNullOrWhiteSpace(title))
            return null;

        return new ReceiptMarket
        {
            Id = marketId,
            Title = title!,
            Slug = receipt.MarketSlugSnapshot,
        };
    }

    private static ReceiptPayment? BuildPayment(
        Receipt receipt,
        IDictionary<Guid, PaymentIntent> paymentLookup
    )
    {
        PaymentIntent? intent = null;
        if (receipt.PaymentIntentId.HasValue)
        {
            paymentLookup.TryGetValue(receipt.PaymentIntentId.Value, out intent);
        }

        var method = receipt.PaymentMethod ?? intent?.PaymentMethod;
        var externalPaymentId =
            receipt.ExternalPaymentId ?? intent?.ExternalPaymentId ?? receipt.ProviderPaymentIdText;
        var checkoutUrl = receipt.CheckoutUrl ?? intent?.CheckoutUrl;
        var expiresAt = receipt.PaymentExpiresAt ?? intent?.ExpiresAt;
        var qrCodeBase64 = intent?.PixQrCodeBase64;

        if (
            method is null
            && externalPaymentId is null
            && checkoutUrl is null
            && expiresAt is null
            && qrCodeBase64 is null
        )
        {
            return null;
        }

        return new ReceiptPayment
        {
            Method = method,
            ExternalPaymentId = externalPaymentId,
            CheckoutUrl = checkoutUrl,
            ExpiresAt = expiresAt,
            QrCodeBase64 = qrCodeBase64,
        };
    }

    private static string? TryExtractDescription(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            if (
                doc.RootElement.TryGetProperty("tx", out var tx)
                && tx.TryGetProperty("Description", out var desc)
                && desc.ValueKind == JsonValueKind.String
            )
            {
                return desc.GetString();
            }

            if (doc.RootElement.TryGetProperty("intent", out var intent))
            {
                if (
                    intent.TryGetProperty("Description", out var intentDesc)
                    && intentDesc.ValueKind == JsonValueKind.String
                )
                {
                    return intentDesc.GetString();
                }

                if (
                    intent.TryGetProperty("PaymentMethod", out var method)
                    && method.ValueKind == JsonValueKind.String
                )
                {
                    return $"Depósito via {method.GetString()}";
                }
            }
        }
        catch
        {
            // ignore parsing issues
        }

        return null;
    }

    private static int? TryGetContracts(Receipt r, string? description)
    {
        var fromPayload = TryGetContractsFromPayload(r.PayloadJson);
        if (fromPayload.HasValue)
            return fromPayload;

        return TryExtractContracts(description ?? r.Description ?? string.Empty);
    }

    private static int? TryGetContractsFromPayload(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            if (doc.RootElement.TryGetProperty("tx", out var tx))
            {
                if (
                    tx.TryGetProperty("contracts", out var contractsProp)
                    && contractsProp.ValueKind == JsonValueKind.Number
                )
                {
                    if (contractsProp.TryGetInt32(out var c) && c > 0)
                        return c;
                }
            }
        }
        catch
        {
            // ignore parsing issues
        }

        return null;
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
    {
        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out userId);
    }

    [GeneratedRegex(@"(\d+)\s+contratos", RegexOptions.IgnoreCase, "pt-BR")]
    private static partial Regex MyRegex();
}
