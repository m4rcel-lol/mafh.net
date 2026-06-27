using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Mail;
using M5FileHost.Core;
using Microsoft.Extensions.Options;

namespace M5FileHost.Web;

public sealed class EmailOptions
{
    public const string Section = "Email";
    public string Host { get; init; } = "";
    [Range(1, 65535)] public int Port { get; init; } = 587;
    public string Username { get; init; } = "";
    public string Password { get; init; } = "";
    public string FromAddress { get; init; } = "";
    public bool EnableSsl { get; init; } = true;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(FromAddress);
}

public interface IAppEmailSender
{
    Task SendPasswordResetAsync(ApplicationUser user, string resetUrl, CancellationToken cancellationToken);
}

public sealed class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger) : IAppEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public async Task SendPasswordResetAsync(ApplicationUser user, string resetUrl, CancellationToken cancellationToken)
    {
        if (!_options.IsConfigured) { logger.LogWarning("Password reset requested, but Email configuration is incomplete"); return; }
        using var message = new MailMessage(_options.FromAddress, user.Email!) { Subject = "Reset your m5 file hoster password", Body = $"A password reset was requested for your account. Open this single-use link:\n\n{resetUrl}\n\nIf you did not request this, ignore this message.", IsBodyHtml = false };
        using var client = new SmtpClient(_options.Host, _options.Port) { EnableSsl = _options.EnableSsl, DeliveryMethod = SmtpDeliveryMethod.Network };
        if (!string.IsNullOrWhiteSpace(_options.Username)) client.Credentials = new NetworkCredential(_options.Username, _options.Password);
        await client.SendMailAsync(message, cancellationToken);
    }
}
