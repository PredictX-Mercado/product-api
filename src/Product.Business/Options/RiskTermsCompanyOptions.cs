namespace Product.Business.Options;

public class RiskTermsCompanyOptions
{
    public const string SectionName = "RiskTerms:Company";

    public string Name { get; set; } = "EMPRESA PADRAO LTDA";
    public string Cnpj { get; set; } = "00.000.000/0000-00";
    public string Address { get; set; } = "Rua Exemplo, 123";
    public string City { get; set; } = "Cidade";
    public string State { get; set; } = "UF";
}
