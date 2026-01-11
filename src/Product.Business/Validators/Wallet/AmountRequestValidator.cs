using FluentValidation;
using Product.Contracts.Wallet;

namespace Product.Business.Validators.Wallet;

public class AmountRequestValidator : AbstractValidator<AmountRequest>
{
    public AmountRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}
