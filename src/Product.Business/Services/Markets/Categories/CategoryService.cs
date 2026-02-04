using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Product.Business.Interfaces.Categories;
using Product.Data.Database.Contexts;
using Product.Data.Models.Markets;
using Product.Data.Models.Markets.Categories;
using MarketEntity = Product.Data.Models.Markets.Market;

namespace Product.Business.Services.Markets.Categories;

public partial class CategoryService(AppDbContext db) : ICategoryService
{
    private readonly AppDbContext _db = db;

    private static string Slugify(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var normalized = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            var uc = char.GetUnicodeCategory(c);
            if (uc != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        var noDiacritics = sb.ToString().Normalize(NormalizationForm.FormC);
        // Replace whitespace runs with single hyphen
        var collapsed = MyRegex().Replace(noDiacritics.Trim(), "-");
        // Remove any characters that are not letters, digits or hyphen
        var cleaned = MyRegex1().Replace(collapsed, string.Empty);
        return cleaned.ToUpperInvariant();
    }

    public async Task EnsureDefaultCategoriesAsync(CancellationToken ct = default)
    {
        var defaults = new[]
        {
            "EM-ALTA",
            "NOVIDADES",
            "TODAS",
            "POLITICA",
            "ESPORTES",
            "CULTURA",
            "CRIPTOMOEDAS",
            "CLIMA",
            "ECONOMIA",
            "MENCOES",
            "EMPRESAS",
            "FINANCAS",
            "TECNOLOGIA-E-CIENCIA",
            "SAUDE",
            "MUNDO",
        };
        foreach (var name in defaults)
        {
            var exists = await _db.Categories.FirstOrDefaultAsync(c => c.Name == name, ct);
            if (exists == null)
            {
                var slug = Slugify(name);
                _db.Categories.Add(
                    new Category
                    {
                        Id = 0,
                        Name = name,
                        Slug = slug,
                    }
                );
            }
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task AssociateCategoryWithMarketAsync(
        MarketEntity market,
        CancellationToken ct = default
    )
    {
        if (market == null)
            return;

        // Ensure defaults exist
        await EnsureDefaultCategoriesAsync(ct);

        // Helper to get or create category by name
        async Task<Category> GetOrCreate(string name)
        {
            var cat = await _db.Categories.FirstOrDefaultAsync(c => c.Name == name, ct);
            if (cat == null)
            {
                cat = new Category
                {
                    Id = 0,
                    Name = name,
                    Slug = Slugify(name),
                };
                _db.Categories.Add(cat);
                await _db.SaveChangesAsync(ct);
            }
            return cat;
        }

        // Always associate with 'todas'
        var todas = await GetOrCreate("TODAS");
        _db.MarketCategories.Add(
            new MarketCategory
            {
                Id = Guid.NewGuid(),
                MarketId = market.Id,
                CategoryId = todas.Id,
            }
        );

        // If market provided a category string, associate it
        if (!string.IsNullOrWhiteSpace(market.Category))
        {
            var name = market.Category;
            var custom = await GetOrCreate(name);
            _db.MarketCategories.Add(
                new MarketCategory
                {
                    Id = Guid.NewGuid(),
                    MarketId = market.Id,
                    CategoryId = custom.Id,
                }
            );
        }

        // Novidades: new markets (created within last 7 days)
        var novidades = await GetOrCreate("NOVIDADES");
        if (market.CreatedAt >= DateTimeOffset.UtcNow.AddDays(-7))
        {
            _db.MarketCategories.Add(
                new MarketCategory
                {
                    Id = Guid.NewGuid(),
                    MarketId = market.Id,
                    CategoryId = novidades.Id,
                }
            );
        }

        // Em alta: simple heuristic using recent volume or contracts
        var emAlta = await GetOrCreate("EM-ALTA");
        if (
            market.Volume24h > 0
            || market.VolumeTotal > 1000
            || market.YesContracts + market.NoContracts > 100
        )
        {
            _db.MarketCategories.Add(
                new MarketCategory
                {
                    Id = Guid.NewGuid(),
                    MarketId = market.Id,
                    CategoryId = emAlta.Id,
                }
            );
        }

        await _db.SaveChangesAsync(ct);
    }

    [GeneratedRegex("\\s+")]
    private static partial Regex MyRegex();

    [GeneratedRegex("[^A-Za-z0-9\\-]")]
    private static partial Regex MyRegex1();
}
