using FluentValidation;
using Product.Contracts.Markets;

namespace Product.Business.Validators.Markets;

public class UpdateMarketRequestValidator : AbstractValidator<UpdateMarketRequest>
{
    public UpdateMarketRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .Length(10, 300)
            .Must(t => !string.IsNullOrWhiteSpace(Sanitize(t)))
            .WithMessage("title_invalid");

        RuleFor(x => x.Description).NotEmpty().Length(50, 5000).WithMessage("description_invalid");

        RuleFor(x => x.Category).NotEmpty().WithMessage("category_required");

        RuleForEach(x => x.Tags).Cascade(CascadeMode.Stop).NotEmpty().MaximumLength(50);

        RuleFor(x => x.Probability).InclusiveBetween(1, 99).WithMessage("probability_invalid");

        RuleFor(x => x.ClosingDate)
            .Must(cd =>
                cd >= DateTimeOffset.UtcNow.AddHours(1) && cd <= DateTimeOffset.UtcNow.AddDays(365)
            )
            .WithMessage("closing_date_invalid");

        RuleFor(x => x.ResolutionDate)
            .Must((req, rd) => rd > req.ClosingDate && rd <= req.ClosingDate.AddDays(365))
            .WithMessage("resolution_date_invalid");

        RuleFor(x => x.ResolutionSource)
            .NotEmpty()
            .MaximumLength(1000)
            .WithMessage("resolution_source_invalid");
    }

    private static string Sanitize(string input)
    {
        return string.Concat(input.Where(c => !char.IsControl(c))).Trim();
    }
}
