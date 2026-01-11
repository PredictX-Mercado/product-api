using FluentValidation;
using Product.Contracts.Users;

namespace Product.Business.Validators.Users;

public class UpdateAvatarRequestValidator : AbstractValidator<UpdateAvatarRequest>
{
    public UpdateAvatarRequestValidator()
    {
        RuleFor(x => x.AvatarUrl).NotEmpty();
    }
}
