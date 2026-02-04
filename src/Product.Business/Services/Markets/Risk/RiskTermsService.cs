using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Product.Business.Interfaces.Market;
using Product.Contracts.Markets;
using Product.Data.Interfaces.Repositories;
using Product.Data.Models.Markets;
using Product.Data.Models.Users;

namespace Product.Business.Services.Markets;

public class RiskTermsService : IRiskTermsService
{
    private const string MarketTitlePlaceholder = "{MARKET_TITLE}";
    private const string UserNamePlaceholder = "{USERNAME}";
    private const string UserFullNamePlaceholder = "{USER_FULLNAME}";
    private const string UserCpfPlaceholder = "{USER_CPF}";
    private const string UserAddressPlaceholder = "{USER_ADDRESS}";
    private const string UserCityPlaceholder = "{USER_CITY}";
    private const string UserStatePlaceholder = "{USER_STATE}";
    private const string UserZipPlaceholder = "{USER_ZIP}";

    private readonly IMarketService _marketService;
    private readonly IRiskTermsRepository _riskTermsRepository;
    private readonly IRiskTermsTemplateRepository _termsTemplateRepository;
    private readonly IUserRepository _userRepository;
    private readonly IRiskTermsPdfGenerator _riskTermsPdfGenerator;

    public RiskTermsService(
        IMarketService marketService,
        IRiskTermsRepository riskTermsRepository,
        IRiskTermsTemplateRepository termsTemplateRepository,
        IUserRepository userRepository,
        IRiskTermsPdfGenerator riskTermsPdfGenerator
    )
    {
        _marketService = marketService;
        _riskTermsRepository = riskTermsRepository;
        _termsTemplateRepository = termsTemplateRepository;
        _userRepository = userRepository;
        _riskTermsPdfGenerator = riskTermsPdfGenerator;
    }

    public async Task<(string TermVersion, string Text)> GetTermsAsync(
        string? version,
        Guid? marketId,
        Guid? userId = null,
        CancellationToken ct = default
    )
    {
        var termVersion = NormalizeVersion(version);
        var text = await ResolveRiskTermTextAsync(termVersion, marketId, ct, userId);
        return (termVersion, text);
    }

    public async Task<RiskTermsResponse> AcceptAsync(
        Guid userId,
        AcceptMarketRiskTermsRequest request,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken ct = default
    )
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));
        if (request.MarketId == Guid.Empty)
            throw new ArgumentException("market_missing", nameof(request.MarketId));

        var termVersion = NormalizeVersion(request.TermVersion);

        var market = await _marketService.GetMarketAsync(request.MarketId, null, ct);
        if (market == null)
            throw new KeyNotFoundException("market_not_found");

        var user = await _userRepository.GetUserWithPersonalDataAsync(userId, ct);
        var userDisplayName = BuildUserDisplayName(user);

        var termText = await ResolveRiskTermTextAsync(termVersion, request.MarketId, ct, userId);

        var snapshot = string.IsNullOrWhiteSpace(request.TermSnapshot)
            ? termText
            : request.TermSnapshot.Trim();
        var hash = string.IsNullOrWhiteSpace(request.TermHash)
            ? ComputeSha256Hex(snapshot)
            : request.TermHash.Trim();

        var normalized = new AcceptMarketRiskTermsRequest
        {
            MarketId = request.MarketId,
            TermVersion = termVersion,
            TermSnapshot = snapshot,
            TermHash = hash,
        };

        var existing = await _riskTermsRepository.GetByUserMarketVersionAsync(
            userId,
            request.MarketId,
            termVersion,
            ct
        );
        if (existing != null)
            return Map(existing);

        var acceptance = new RiskTerms
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MarketId = request.MarketId,
            TermVersion = termVersion,
            TermSnapshot = string.IsNullOrWhiteSpace(normalized.TermSnapshot)
                ? null
                : normalized.TermSnapshot.Trim(),
            TermHash = string.IsNullOrWhiteSpace(normalized.TermHash)
                ? null
                : normalized.TermHash.Trim(),
            IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? null : ipAddress.Trim(),
            UserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent.Trim(),
            AcceptedAt = DateTimeOffset.UtcNow,
        };

        await _riskTermsRepository.AddAsync(acceptance, ct);
        return Map(acceptance);
    }

    public async Task<RiskTermsResponse?> GetAcceptanceAsync(
        Guid userId,
        Guid? marketId,
        CancellationToken ct = default
    )
    {
        RiskTerms? acceptance;
        if (marketId.HasValue && marketId.Value != Guid.Empty)
        {
            acceptance = await _riskTermsRepository.GetLatestByUserMarketAsync(
                userId,
                marketId.Value,
                ct
            );
        }
        else
        {
            acceptance = await _riskTermsRepository.GetLatestByUserAsync(userId, ct);
        }

        return acceptance == null ? null : Map(acceptance);
    }

    public async Task<(RiskTermsResponse Acceptance, string Text)?> GetAcceptanceWithTextAsync(
        Guid userId,
        Guid? marketId,
        CancellationToken ct = default
    )
    {
        RiskTerms? acceptance;
        if (marketId.HasValue && marketId.Value != Guid.Empty)
        {
            acceptance = await _riskTermsRepository.GetLatestByUserMarketAsync(
                userId,
                marketId.Value,
                ct
            );
        }
        else
        {
            acceptance = await _riskTermsRepository.GetLatestByUserAsync(userId, ct);
        }

        if (acceptance == null)
            return null;

        var text = acceptance.TermSnapshot;
        if (string.IsNullOrWhiteSpace(text))
            text = await ResolveRiskTermTextAsync(
                acceptance.TermVersion,
                marketId,
                ct,
                acceptance.UserId
            );

        return (Map(acceptance), text);
    }

    public async Task<(string FileName, byte[] Content)> GetAcceptancePdfAsync(
        Guid userId,
        Guid? marketId,
        string? username,
        CancellationToken ct = default
    )
    {
        var result = await GetAcceptanceWithTextAsync(userId, marketId, ct);
        if (result == null)
            throw new KeyNotFoundException("acceptance_not_found");

        var acceptance = result.Value.Acceptance;
        var text = result.Value.Text;
        var marketTitle = await TryGetMarketTitleAsync(acceptance.MarketId, ct);

        // tentar obter e-mail e CPF do USUÁRIO para incluir no comprovante
        var user = await _userRepository.GetUserWithPersonalDataAsync(acceptance.UserId, ct);
        var userEmail = user?.Email;
        var userCpf = user?.PersonalData?.Cpf;

        var displayName = BuildUserDisplayName(user);
        var fileName = BuildAcceptanceFileName(displayName, acceptance.AcceptedAt);

        var maskedCpf = MaskCpf(userCpf);
        var pdfBytes = _riskTermsPdfGenerator.BuildPdf(
            text,
            acceptance,
            marketTitle,
            displayName,
            userEmail,
            maskedCpf
        );
        return (fileName, pdfBytes);
    }

    private string NormalizeVersion(string? version)
    {
        return string.IsNullOrWhiteSpace(version)
            ? _termsTemplateRepository.DefaultVersion
            : version.Trim();
    }

    private async Task<string> ResolveRiskTermTextAsync(
        string version,
        Guid? marketId,
        CancellationToken ct,
        Guid? userId
    )
    {
        var template = await _termsTemplateRepository.GetTermsTemplateAsync(version, ct);

        var title = await TryGetMarketTitleAsync(marketId, ct);

        var filled = template.Replace(MarketTitlePlaceholder, title ?? "-");

        string fullName = "USUÁRIO";
        string cpf = null!;
        string address = "-";
        string city = "-";
        string state = "-";
        string zip = "-";

        if (userId.HasValue && userId.Value != Guid.Empty)
        {
            var user = await _userRepository.GetUserWithPersonalDataAsync(userId.Value, ct);
            if (user != null)
            {
                fullName = !string.IsNullOrWhiteSpace(user.Name)
                    ? user.Name.Trim()
                    : (user.Email ?? "USUÁRIO");
                cpf = user.PersonalData?.Cpf!;
                var addr = user.PersonalData?.Address;
                if (addr != null)
                {
                    var number = string.IsNullOrWhiteSpace(addr.Number) ? "" : $", {addr.Number}";
                    var complement = string.IsNullOrWhiteSpace(addr.Complement)
                        ? ""
                        : $" ({addr.Complement})";
                    address = $"{addr.Street}{number}{complement} - {addr.City}/{addr.State}";
                    city = string.IsNullOrWhiteSpace(addr.City) ? "-" : addr.City.Trim();
                    state = string.IsNullOrWhiteSpace(addr.State) ? "-" : addr.State.Trim();
                    zip = string.IsNullOrWhiteSpace(addr.ZipCode) ? "-" : addr.ZipCode.Trim();
                }
                string userIdText = user.Id.ToString();
            }
        }

        // Substitui��es: manter o corpo geral com 'USUÁRIO' onde aplic�vel
        filled = filled.Replace(UserNamePlaceholder, "USUÁRIO");
        filled = filled.Replace(UserFullNamePlaceholder, fullName);
        filled = filled.Replace(UserCpfPlaceholder, MaskCpf(cpf));
        filled = filled.Replace(UserAddressPlaceholder, address);
        filled = filled.Replace(UserCityPlaceholder, city);
        filled = filled.Replace(UserStatePlaceholder, state);
        filled = filled.Replace(UserZipPlaceholder, zip);

        return filled;
    }

    private async Task<string?> TryGetMarketTitleAsync(Guid? marketId, CancellationToken ct)
    {
        if (marketId != null && marketId != Guid.Empty)
        {
            var market = await _marketService.GetMarketAsync(marketId.Value, null, ct);
            if (market != null && !string.IsNullOrWhiteSpace(market.Title))
                return market.Title.Trim();
        }
        return null;
    }

    private string BuildUserDisplayName(ApplicationUser? user)
    {
        if (user == null)
            return "USUARIO";
        if (!string.IsNullOrWhiteSpace(user.Name))
            return user.Name.Trim();
        if (!string.IsNullOrWhiteSpace(user.Email))
            return user.Email.Trim();
        return "USUARIO";
    }

    private async Task<string?> TryGetUserNameAsync(Guid userId, CancellationToken ct)
    {
        var user = await _userRepository.GetUserWithPersonalDataAsync(userId, ct);
        return BuildUserDisplayName(user);
    }

    private static string ComputeSha256Hex(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input ?? string.Empty);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static RiskTermsResponse Map(RiskTerms acceptance)
    {
        return new RiskTermsResponse
        {
            Id = acceptance.Id,
            UserId = acceptance.UserId,
            MarketId = acceptance.MarketId,
            TermVersion = acceptance.TermVersion,
            AcceptedAt = acceptance.AcceptedAt,
            TermSnapshot = acceptance.TermSnapshot,
            TermHash = acceptance.TermHash,
        };
    }

    private static string MaskCpf(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf))
            return "-";

        var digits = new string(cpf.Where(char.IsDigit).ToArray());
        if (digits.Length < 5)
            return cpf;

        var last5 = digits[^5..];
        var last3 = last5[..3];
        var last2 = last5[^2..];

        return $"***.***.{last3}-{last2}";
    }

    private static string BuildAcceptanceFileName(string? username, DateTimeOffset acceptedAt)
    {
        var namePart = string.IsNullOrWhiteSpace(username)
            ? "usuario"
            : username!.Trim().ToLowerInvariant();

        namePart = Regex.Replace(namePart, @"[^\w\d-]+", "-");
        namePart = Regex.Replace(namePart, "-{2,}", "-").Trim('-');
        if (string.IsNullOrWhiteSpace(namePart))
            namePart = "usuario";

        var datePart = acceptedAt.ToOffset(TimeSpan.FromHours(-3)).ToString("dd-MM-yyyy");
        return $"aceite-termo-de-risco-{namePart}-{datePart}.pdf";
    }
}
