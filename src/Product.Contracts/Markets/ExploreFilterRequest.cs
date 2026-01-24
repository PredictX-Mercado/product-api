namespace Product.Contracts.Markets;

public class ExploreFilterRequest
{
    public string? Search { get; set; }
    public string? Category { get; set; }
    public string[]? Categories { get; set; }
    public string? Sort { get; set; }
    public (decimal? Min, decimal? Max)? ProbabilityRange { get; set; }
    public string? ClosingRange { get; set; }
    public string? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
