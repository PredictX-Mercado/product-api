namespace Product.Contracts.Payments;

public record CreatePixRequest(
    decimal Amount,
    string Description,
    string OrderId,
    string BuyerEmail,
    int? ExpirationMinutes = null
);

public record CreateCardRequest(
    decimal Amount,
    string Description,
    string OrderId,
    string BuyerEmail,
    string Token,
    int Installments,
    string PaymentMethodId
);

public record CreateBoletoRequest(
    decimal Amount,
    string Description,
    string OrderId,
    string BuyerEmail,
    string FirstName,
    string LastName,
    string IdentificationType,
    string IdentificationNumber
);

public record PixResponse(
    long PaymentId,
    string? QrCodeBase64,
    string? QrCode,
    DateTimeOffset? ExpiresAt,
    string Status
);

public record PaymentStatusResponse(
    long PaymentId,
    string Status,
    string? StatusDetail,
    string? ExternalReference,
    decimal? Amount,
    DateTimeOffset? ExpiresAt
);
