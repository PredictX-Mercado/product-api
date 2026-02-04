using Product.Contracts.Markets;

namespace Product.Business.Interfaces.Market;

public interface IRiskTermsPdfGenerator
{
    byte[] BuildPdf(
        string body,
        RiskTermsResponse acceptance,
        string? marketTitle,
        string? username,
        string? userEmail,
        string? maskedUserCpf
    );
}
