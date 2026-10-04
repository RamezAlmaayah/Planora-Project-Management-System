using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;
using MimeKit;
using Planora.Infrastructure.Email;
using Planora.Web.Email;

namespace Planora.IntegrationTests;

public sealed class SmtpEmailServiceTests
{
    [Fact]
    public async Task SendEmailAsync_SupportsLocalSmtpWithoutAuthentication()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        Task<string> server = RunUnauthenticatedSmtpServerAsync(listener, timeout.Token);
        var service = new SmtpEmailService(Options.Create(new EmailOptions
        {
            Host = IPAddress.Loopback.ToString(),
            Port = port,
            FromEmail = "planora@example.test",
            FromName = "Planora",
            UseSsl = false
        }));

        await service.SendEmailAsync(
            "recipient@example.test",
            PlanoraEmailTemplates.ConfirmationSubject,
            PlanoraEmailTemplates.Confirmation("Local Test", "https://planora.example/confirm?code=test-token"),
            timeout.Token,
            PlanoraEmailTemplates.ConfirmationIllustration);
        string capturedMessage = await server;
        var parsedMessage = MimeMessage.Load(
            new MemoryStream(Encoding.ASCII.GetBytes(capturedMessage)));
        string capturedHtml = parsedMessage.HtmlBody!;

        Assert.Contains("Confirm your Planora email", parsedMessage.Subject);
        Assert.Contains("Confirm Email", capturedHtml);
        Assert.Contains("Project Management Platform", capturedHtml);
        Assert.Contains("https://planora.example/confirm?code=test-token", capturedHtml);
        Assert.Contains("cid:planora-confirm-email", capturedHtml);
        var illustrationPart = parsedMessage.BodyParts
            .OfType<MimePart>()
            .Single(part => part.ContentId == "planora-confirm-email");
        Assert.Equal("image/png", illustrationPart.ContentType.MimeType);
        Assert.Equal("inline", illustrationPart.ContentDisposition?.Disposition);
    }

    private static async Task<string> RunUnauthenticatedSmtpServerAsync(
        TcpListener listener,
        CancellationToken cancellationToken)
    {
        using TcpClient client = await listener.AcceptTcpClientAsync(cancellationToken);
        await using NetworkStream stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        await using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true)
        {
            NewLine = "\r\n",
            AutoFlush = true
        };
        var capturedMessage = new StringBuilder();

        await writer.WriteLineAsync("220 localhost ESMTP ready");
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            string command = line.ToUpperInvariant();
            if (command.StartsWith("EHLO ", StringComparison.Ordinal) ||
                command.StartsWith("HELO ", StringComparison.Ordinal))
            {
                await writer.WriteLineAsync("250 localhost");
            }
            else if (command.StartsWith("MAIL FROM:", StringComparison.Ordinal) ||
                     command.StartsWith("RCPT TO:", StringComparison.Ordinal))
            {
                await writer.WriteLineAsync("250 OK");
            }
            else if (command == "DATA")
            {
                await writer.WriteLineAsync("354 End data with <CR><LF>.<CR><LF>");
                while (await reader.ReadLineAsync(cancellationToken) is { } dataLine && dataLine != ".")
                {
                    capturedMessage.AppendLine(dataLine);
                }
                await writer.WriteLineAsync("250 Message accepted");
            }
            else if (command == "QUIT")
            {
                await writer.WriteLineAsync("221 Bye");
                return capturedMessage.ToString();
            }
            else
            {
                await writer.WriteLineAsync("500 Unsupported command");
            }
        }

        return capturedMessage.ToString();
    }
}
