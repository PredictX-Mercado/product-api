namespace Product.Business.Interfaces.Market;

public interface IRiskTermsTemplateRepository
{
    string DefaultVersion { get; }
    Task<string> GetTermsTemplateAsync(string version, CancellationToken ct = default);
}
