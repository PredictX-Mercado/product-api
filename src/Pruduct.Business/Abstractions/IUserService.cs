using Pruduct.Business.Abstractions.Results;
using Pruduct.Contracts.Auth;
using Pruduct.Contracts.Users;

namespace Pruduct.Business.Abstractions;

public interface IUserService
{
    Task<ServiceResult<UserView>> GetMeAsync(Guid userId, CancellationToken ct = default);
    Task<ServiceResult<UserView>> UpdateMeAsync(
        Guid userId,
        UpdateMeRequest request,
        CancellationToken ct = default
    );
}
