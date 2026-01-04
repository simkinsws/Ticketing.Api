using System.Net;
using System.Net.Mail;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace Ticketing.Api.Services;

public interface IEmailService
{
    Task SendConfirmationEmailAsync(string email, string confirmationLink);
    Task SendPasswordResetEmailAsync(string email, string resetLink);
}

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendConfirmationEmailAsync(string email, string confirmationLink)
    {
        var subject = "Confirm Your Email Address";
        var htmlBody = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                <h2 style='color: #333;'>Welcome to Ticketing System!</h2>
                <p style='color: #666; font-size: 16px;'>Please confirm your email address by clicking the button below:</p>
                <p style='margin-top: 30px;'>
                    <a href='{confirmationLink}' 
                       style='background-color: #007bff; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block;'>
                        Confirm Email
                    </a>
                </p>
                <p style='color: #999; font-size: 12px;'>This link expires in 24 hours.</p>
            </div>
        ";

        await SendEmailAsync(email, subject, htmlBody);
    }

    public async Task SendPasswordResetEmailAsync(string email, string resetLink)
    {
        var subject = "Reset Your Password";
        var htmlBody = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                <h2 style='color: #333;'>Password Reset Request</h2>
                <p style='color: #666; font-size: 16px;'>Click the button below to reset your password:</p>
                <p style='margin-top: 30px;'>
                    <a href='{resetLink}' 
                       style='background-color: #007bff; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block;'>
                        Reset Password
                    </a>
                </p>
                <p style='color: #999; font-size: 12px;'>This link expires in 24 hours.</p>
            </div>
        ";

        await SendEmailAsync(email, subject, htmlBody);
    }

    private async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        try
        {
            //TODO: Move to SendGrid settings class and bind in DI.
            var apiKey = _config["SendGridSettings:ApiKey"];
            var fromEmail = _config["SendGridSettings:FromEmail"] ?? "noreply@ticketing.local";
            var fromName = _config["SendGridSettings:FromName"] ?? "Ticketing System";

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("SendGrid API key is not configured. Email to {Email} was not sent.", toEmail);
                _logger.LogWarning("Configure 'SendGridSettings:ApiKey' in appsettings.json or environment variable");
                return;
            }

            var options = new SendGridClientOptions
            {
                ApiKey = apiKey
            };

            var client = new SendGridClient(options);
            var from = new EmailAddress(fromEmail, fromName);
            var to = new EmailAddress(toEmail);
            var msg = MailHelper.CreateSingleEmail(from, to, subject, null, htmlBody);

            var response = await client.SendEmailAsync(msg);

            if (response.StatusCode == System.Net.HttpStatusCode.Accepted)
            {
                _logger.LogInformation("Email sent successfully to {Email} via SendGrid", toEmail);
            }
            else
            {
                _logger.LogError("SendGrid returned status code {StatusCode} for email to {Email}. Response: {ResponseContent}", 
                    response.StatusCode, toEmail, response.Body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email} via SendGrid", toEmail);
        }
    }
}
