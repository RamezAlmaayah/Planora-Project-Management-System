namespace Planora.Application.Abstractions.Common;

public interface IEmailService
{
    Task SendEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default,
        EmailInlineResource? inlineResource = null);
}
