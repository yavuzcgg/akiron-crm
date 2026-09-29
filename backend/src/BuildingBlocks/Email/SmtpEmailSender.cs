using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Akiron.BuildingBlocks.Email;

/// <summary>Bound from <c>Email:Smtp</c>. Mailpit locally; any SMTP relay (SES, Resend, Postmark) in production.</summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Email:Smtp";

    public string? Host { get; set; }

    public int Port { get; set; } = 587;

    /// <summary>True for STARTTLS/SSL relays; false only for a local catcher such as Mailpit.</summary>
    public bool UseTls { get; set; } = true;

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string FromAddress { get; set; } = "no-reply@akiron.local";

    public string FromName { get; set; } = "Akiron CRM";
}

/// <summary>Sends through SMTP, one connection per message (volumes are small; a pool comes with a queue).</summary>
public sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var smtp = options.Value;
        var host = smtp.Host ?? throw new InvalidOperationException("Email:Smtp:Host is not configured.");
        using var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(smtp.FromName, smtp.FromAddress));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new TextPart("plain") { Text = message.TextBody };

        using var client = new SmtpClient();
        await client.ConnectAsync(host, smtp.Port, smtp.UseTls ? SecureSocketOptions.StartTlsWhenAvailable : SecureSocketOptions.None, cancellationToken);

        if (!string.IsNullOrEmpty(smtp.Username))
        {
            await client.AuthenticateAsync(smtp.Username, smtp.Password ?? string.Empty, cancellationToken);
        }

        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}
