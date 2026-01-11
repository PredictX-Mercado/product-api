using Product.Data.Models.Emails;

namespace Product.Business.Interfaces.Email;

public interface IEmailService
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}
