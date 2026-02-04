using Microsoft.Extensions.Options;
using Product.Business.Interfaces.Email;
using Product.Business.Options;
using Product.Data.Models.Emails;

namespace Product.Business.Services.Mailers;

public class QueuedEmailSender : IEmailSender, Product.Business.Interfaces.Auth.IEmailSender
{
    private readonly IEmailQueue _queue;
    private readonly IEmailTemplateRenderer _renderer;
    private readonly EmailOptions _options;

    public QueuedEmailSender(
        IEmailQueue queue,
        IEmailTemplateRenderer renderer,
        IOptions<EmailOptions> options
    )
    {
        _queue = queue;
        _renderer = renderer;
        _options = options.Value;
    }

    // Implementation for generic mailer interface used elsewhere in the app
    public async Task SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        CancellationToken ct = default
    )
    {
        await _queue.EnqueueAsync(new EmailMessage(toEmail, toEmail, subject, htmlBody), ct);
    }

    public async Task SendEmailVerificationAsync(
        string toEmail,
        string userName,
        string confirmUrl,
        CancellationToken ct = default
    )
    {
        var model = new ConfirmationEmailModel { UserName = userName, ConfirmUrl = confirmUrl };
        var html = await _renderer.RenderAsync("ConfirmationEmail", model, ct);
        await _queue.EnqueueAsync(
            new EmailMessage(toEmail, userName, "Confirme seu e-mail", html),
            ct
        );
    }

    public async Task SendChangeEmailAsync(
        string toEmail,
        string userName,
        string confirmUrl,
        CancellationToken ct = default
    )
    {
        var model = new ChangeEmailModel { UserName = userName, ConfirmUrl = confirmUrl };
        var html = await _renderer.RenderAsync("ChangeEmail", model, ct);
        await _queue.EnqueueAsync(
            new EmailMessage(
                toEmail,
                userName,
                "Confirme a alteração de e-mail",
                html
            ),
            ct
        );
    }

    public async Task SendForgotPasswordAsync(
        string toEmail,
        string userName,
        string resetCode,
        CancellationToken ct = default
    )
    {
        var model = new ForgotPasswordEmailModel { UserName = userName, ResetCode = resetCode };
        var html = await _renderer.RenderAsync("ForgotPassword", model, ct);
        await _queue.EnqueueAsync(
            new EmailMessage(toEmail, userName, "Código para redefinir sua senha", html),
            ct
        );
    }

    public async Task SendResetPasswordConfirmationAsync(
        string toEmail,
        string userName,
        CancellationToken ct = default
    )
    {
        var model = new ResetPasswordConfirmationModel { UserName = userName };
        var html = await _renderer.RenderAsync("ConfirmResetPassword", model, ct);
        await _queue.EnqueueAsync(
            new EmailMessage(toEmail, userName, "Sua senha foi atualizada", html),
            ct
        );
    }
}
