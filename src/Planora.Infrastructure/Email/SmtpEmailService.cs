using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using Planora.Application.Abstractions.Common;

namespace Planora.Infrastructure.Email;

public sealed class SmtpEmailService : IEmailService
{
    private readonly EmailOptions _options;

    public SmtpEmailService(
        IOptions<EmailOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default,
        EmailInlineResource? inlineResource = null)
    {
        var message = new MimeMessage();

        message.From.Add(
            new MailboxAddress(
                _options.FromName,
                _options.FromEmail));

        message.To.Add(
            MailboxAddress.Parse(toEmail));

        message.Subject = subject;

        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = htmlBody
        };

        if (inlineResource is not null)
        {
            var linkedResource = bodyBuilder.LinkedResources.Add(
                inlineResource.ContentId,
                new MemoryStream(inlineResource.Content, writable: false),
                ContentType.Parse(inlineResource.MediaType));
            linkedResource.ContentId = inlineResource.ContentId;
            linkedResource.ContentDisposition = new ContentDisposition(ContentDisposition.Inline);
        }

        message.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();

        var secureSocketOptions = _options.UseSsl
            ? SecureSocketOptions.StartTls
            : SecureSocketOptions.Auto;

        await client.ConnectAsync(
            _options.Host,
            _options.Port,
            secureSocketOptions,
            cancellationToken);

        bool hasUserName = !string.IsNullOrWhiteSpace(_options.UserName);
        bool hasPassword = !string.IsNullOrWhiteSpace(_options.Password);
        if (hasUserName != hasPassword)
        {
            throw new InvalidOperationException(
                "Configure both SMTP username and password, or leave both empty for an unauthenticated local SMTP server.");
        }

        if (hasUserName)
        {
            await client.AuthenticateAsync(
                _options.UserName,
                _options.Password,
                cancellationToken);
        }

        await client.SendAsync(
            message,
            cancellationToken);

        await client.DisconnectAsync(
            true,
            cancellationToken);
    }
}
