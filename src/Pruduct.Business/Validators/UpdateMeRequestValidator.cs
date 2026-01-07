using FluentValidation;
using Pruduct.Contracts.Users;

namespace Pruduct.Business.Validators;

public class UpdateMeRequestValidator : AbstractValidator<UpdateMeRequest>
{
    public UpdateMeRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress();

        When(
            x => !string.IsNullOrWhiteSpace(x.Password),
            () =>
            {
                RuleFor(x => x.Password).MinimumLength(6);
                RuleFor(x => x.ConfirmPassword)
                    .Equal(x => x.Password)
                    .WithMessage("Passwords must match");
            }
        );
    }
}
