using Resend;

namespace Task_4.Services;

public interface IEmailService
{
    Task SendVerificationEmailAsync(string toEmail, string toName, string verificationLink);
}

public class EmailService : IEmailService
{
    private readonly IResend _resend;
    private readonly ILogger<EmailService> _logger;
    private readonly IConfiguration _configuration;

    public EmailService(IResend resend, ILogger<EmailService> logger, IConfiguration configuration)
    {
        _resend = resend;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task SendVerificationEmailAsync(string toEmail, string toName, string verificationLink)
    {
        try
        {
            var fromEmail = _configuration["Email:FromEmail"] ?? "onboarding@resend.dev";
            var fromName = _configuration["Email:FromName"] ?? "Task 4 App";

            var message = new EmailMessage
            {
                From = fromEmail,
                To = new[] { toEmail },
                Subject = "Verify Your Email Address",
                HtmlBody = GenerateVerificationEmailHtml(toName, verificationLink)
            };

            await _resend.EmailSendAsync(message);
            _logger.LogInformation("Verification email sent to {Email}", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send verification email to {Email}", toEmail);
            throw;
        }
    }

    private string GenerateVerificationEmailHtml(string name, string verificationLink)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #007bff; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
        .content {{ background-color: #f8f9fa; padding: 30px; border: 1px solid #dee2e6; }}
        .button {{ display: inline-block; padding: 12px 30px; background-color: #28a745; color: white; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
        .footer {{ text-align: center; padding: 20px; color: #6c757d; font-size: 14px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Welcome to Task 4 App!</h1>
        </div>
        <div class='content'>
            <h2>Hello {name},</h2>
            <p>Thank you for registering with us! Please verify your email address to complete your registration.</p>
            <p>Click the button below to verify your email:</p>
            <div style='text-align: center;'>
                <a href='{verificationLink}' class='button'>Verify Email Address</a>
            </div>
            <p>Or copy and paste this link into your browser:</p>
            <p style='word-break: break-all; color: #007bff;'>{verificationLink}</p>
            <p><strong>Note:</strong> This link will expire in 24 hours.</p>
            <p>If you didn't create an account with us, please ignore this email.</p>
        </div>
        <div class='footer'>
            <p>&copy; 2026 Task 4 App. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";
    }
}