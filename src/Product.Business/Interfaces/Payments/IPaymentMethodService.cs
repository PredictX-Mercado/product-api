using Product.Business.Interfaces.Results;
using Product.Contracts.Payments;

namespace Product.Business.Interfaces.Payments;

public interface IPaymentMethodService
{
    Task<ServiceResult<PaymentMethodListResponse>> GetMethodsAsync(
        Guid userId,
        CancellationToken ct = default
    );

    Task<ServiceResult<PaymentMethodResponse>> CreateMethodAsync(
        Guid userId,
        CreatePaymentMethodRequest request,
        CancellationToken ct = default
    );

    Task<ServiceResult<bool>> DeleteMethodAsync(
        Guid userId,
        Guid methodId,
        CancellationToken ct = default
    );
}
