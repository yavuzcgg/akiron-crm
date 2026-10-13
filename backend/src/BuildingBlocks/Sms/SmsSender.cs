using Microsoft.Extensions.Logging;

namespace Akiron.BuildingBlocks.Sms;

/// <param name="To">E.164, e.g. <c>+905321234567</c>; see <see cref="TurkishPhone"/>.</param>
public sealed record SmsMessage(string To, string Text);

/// <summary>
/// Outgoing SMS (ADR-0005: every outside service sits behind an interface). Until an agency's own
/// provider account is connected (Netgsm first, with per-tenant credentials), messages go to the
/// logging sender: everything that would send an SMS works and is visible, nothing leaves.
/// </summary>
public interface ISmsSender
{
    Task SendAsync(SmsMessage message, CancellationToken cancellationToken);
}

/// <summary>Stand-in without a provider: writes the message to the log.</summary>
public sealed partial class LoggingSmsSender(ILogger<LoggingSmsSender> logger) : ISmsSender
{
    public Task SendAsync(SmsMessage message, CancellationToken cancellationToken)
    {
        LogSms(logger, message.To, message.Text);
        return Task.CompletedTask;
    }

    [LoggerMessage(EventId = 4100, Level = LogLevel.Information, Message = "SMS to {To}: {Text}")]
    private static partial void LogSms(ILogger logger, string to, string text);
}

/// <summary>Turkish mobile numbers as people type them, turned into the one form providers accept.</summary>
public static class TurkishPhone
{
    /// <summary>The longest text sent: three concatenated parts in the GSM alphabet.</summary>
    public const int MaxTextLength = 459;

    /// <summary>
    /// <c>0532 123 45 67</c>, <c>+90 (532) 123-45-67</c>, <c>5321234567</c> → <c>+905321234567</c>.
    /// False for anything that is not a Turkish mobile number (land lines cannot receive SMS).
    /// </summary>
    public static bool TryNormalize(string? input, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var digits = new string(input.Where(char.IsAsciiDigit).ToArray());
        if (digits.StartsWith("0090", StringComparison.Ordinal))
        {
            digits = digits[4..];
        }
        else if (digits.StartsWith("90", StringComparison.Ordinal) && digits.Length == 12)
        {
            digits = digits[2..];
        }
        else if (digits.StartsWith('0'))
        {
            digits = digits[1..];
        }

        if (digits.Length != 10 || digits[0] != '5')
        {
            return false;
        }

        normalized = "+90" + digits;
        return true;
    }
}
