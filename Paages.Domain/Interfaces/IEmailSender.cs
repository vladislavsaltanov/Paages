namespace Paages.Domain.Interfaces;

public interface IEmailSender
{
    Task SendEmailConfirmationAsync(string toEmail, string confirmationLink);
    Task SendPasswordResetAsync(string toEmail, string resetLink);
    Task SendEmailChangeAsync(string toEmail, string confirmationLink);
    Task SendSecurityAlertAsync(string toEmail, string message);
}