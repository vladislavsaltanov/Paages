using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using Paages.Domain.Interfaces;

namespace Paages.Infrastructure.Email;

public class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    public Task SendEmailConfirmationAsync(string toEmail, string link) => SendAsync(toEmail, "Подтвердите почту - Paages",
        $"<p>Перейдите по ссылке, чтобы подтвердить почту:</p><p><a href=\"{link}\">{link}</a></p><p>Ссылка действует 24 часа.</p>");

    public Task SendPasswordResetAsync(string toEmail, string link) => SendAsync(toEmail, "Сброс пароля - Paages",
        $"<p>Перейдите по ссылке, чтобы сбросить пароль:</p><p><a href=\"{link}\">{link}</a></p><p>Если это были не вы - проигнорируйте письмо. Ссылка действует 24 часа.</p>");

    public Task SendEmailChangeAsync(string toEmail, string link) => SendAsync(toEmail, "Подтвердите новую почту - Paages",
        $"<p>Перейдите по ссылке, чтобы подтвердить новый адрес для аккаунта Paages:</p><p><a href=\"{link}\">{link}</a></p><p>Ссылка действует 24 часа.</p>");

    public Task SendSecurityAlertAsync(string toEmail, string message) => SendAsync(toEmail, "В аккаунте произошли изменения - Paages",
        $"<p>{message}</p><p>Если это были не вы - срочно смените пароль ещё раз.</p>");

    private async Task SendAsync(string toEmail, string subject, string html)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(options.Value.FromName, options.Value.FromAddress));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = html };

        using var client = new SmtpClient();
        await client.ConnectAsync(options.Value.Host, options.Value.Port, SecureSocketOptions.SslOnConnect);
        await client.AuthenticateAsync(options.Value.Username, options.Value.Password);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
