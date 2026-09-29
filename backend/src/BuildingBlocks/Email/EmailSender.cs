using Microsoft.Extensions.Logging;

namespace Akiron.BuildingBlocks.Email;

public sealed record EmailMessage(string To, string Subject, string TextBody);

/// <summary>Outgoing transactional e-mail (ADR-0005). A real provider (Resend/SES/SMTP) replaces the logging one in Sprint 2b.</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

/// <summary>Development stand-in: writes the whole message, links included, to the log.</summary>
public sealed partial class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        LogEmail(logger, message.To, message.Subject, message.TextBody);
        return Task.CompletedTask;
    }

    [LoggerMessage(EventId = 4000, Level = LogLevel.Information, Message = "E-mail to {To}: {Subject}\n{Body}")]
    private static partial void LogEmail(ILogger logger, string to, string subject, string body);
}
