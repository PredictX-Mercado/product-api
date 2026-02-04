using Microsoft.AspNetCore.Hosting;
using Product.Business.Interfaces.Market;
using Product.Business.Options;
using Microsoft.Extensions.Options;

namespace Product.Api.Services;

public class FileRiskTermsTemplateRepository : IRiskTermsTemplateRepository
{
    private readonly IWebHostEnvironment _env;
    private readonly RiskTermsCompanyOptions _company;
    private readonly Dictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public FileRiskTermsTemplateRepository(
        IWebHostEnvironment env,
        IOptions<RiskTermsCompanyOptions> companyOptions
    )
    {
        _env = env;
        _company = companyOptions?.Value ?? new RiskTermsCompanyOptions();
    }

    public string DefaultVersion => "v1";

    public async Task<string> GetTermsTemplateAsync(
        string version,
        CancellationToken ct = default
    )
    {
        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("unknown_term_version");

        lock (_lock)
        {
            if (_cache.TryGetValue(version, out var cached))
                return cached;
        }

        var path = BuildPath(version);
        if (!File.Exists(path))
            throw new ArgumentException("unknown_term_version");

        var text = await File.ReadAllTextAsync(path, ct);

        lock (_lock)
        {
            _cache[version] = text;
        }

        return InjectCompanyPlaceholders(text);
    }

    private string BuildPath(string version)
    {
        return Path.Combine(
            _env.ContentRootPath,
            "Resources",
            "terms",
            "risk",
            $"{version}.txt"
        );
    }

    private string InjectCompanyPlaceholders(string text)
    {
        return text
            .Replace("{COMPANY_NAME}", _company.Name ?? string.Empty)
            .Replace("{COMPANY_CNPJ}", _company.Cnpj ?? string.Empty)
            .Replace("{COMPANY_ADDRESS}", _company.Address ?? string.Empty)
            .Replace("{COMPANY_CITY}", _company.City ?? string.Empty)
            .Replace("{COMPANY_STATE}", _company.State ?? string.Empty);
    }
}
