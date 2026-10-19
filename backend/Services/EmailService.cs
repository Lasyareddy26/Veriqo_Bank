using Veriqo.Api.Settings;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Veriqo.Api.Services;

public interface IEmailService
{
    bool Configured { get; }
    Task<bool> SendOtpAsync(string toEmail, string otp);
}

/// <summary>
/// Sends OTP emails over SMTP (MailKit). When SMTP is not configured, it logs the OTP
/// instead and reports "not sent" so the caller can surface a dev code — the verification
/// flow stays fully testable without real mail credentials.
/// </summary>
public class SmtpEmailService : IEmailService
{
    private readonly SmtpSettings _smtp;
    private readonly ILogger<SmtpEmailService> _log;

    public SmtpEmailService(IOptions<SmtpSettings> smtp, ILogger<SmtpEmailService> log)
    {
        _smtp = smtp.Value;
        _log = log;
    }

    public bool Configured => _smtp.Configured;

    public async Task<bool> SendOtpAsync(string toEmail, string otp)
    {
        if (!_smtp.Configured)
        {
            _log.LogWarning("SMTP not configured — OTP for {Email} is {Otp} (dev mode)", toEmail, otp);
            return false;
        }

        try
        {
            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress(_smtp.FromName, _smtp.FromEmail));
            msg.To.Add(MailboxAddress.Parse(toEmail));
            msg.Subject = "Your Veriqo verification code";
            msg.Body = new BodyBuilder
            {
                HtmlBody = $@"
                    <div style='font-family:Inter,Arial,sans-serif;max-width:480px;margin:auto'>
                      <h2 style='color:#262520'>Verify your email</h2>
                      <p style='color:#4A483F'>Use this code to finish setting up your Veriqo account. It expires in 10 minutes.</p>
                      <div style='font-size:32px;font-weight:700;letter-spacing:8px;color:#C4603C;padding:16px 0'>{otp}</div>
                      <p style='color:#77756B;font-size:13px'>If you didn't request this, you can ignore this email.</p>
                    </div>",
                TextBody = $"Your Veriqo verification code is {otp}. It expires in 10 minutes."
            }.ToMessageBody();

            using var client = new SmtpClient();
            // CRL/OCSP revocation endpoints are often unreachable in dev (esp. macOS),
            // which makes the TLS handshake fail. Skip revocation but keep chain validation.
            client.CheckCertificateRevocation = false;
            await client.ConnectAsync(_smtp.Host, _smtp.Port,
                _smtp.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.SslOnConnect);
            await client.AuthenticateAsync(_smtp.User, _smtp.Password);
            await client.SendAsync(msg);
            await client.DisconnectAsync(true);
            _log.LogInformation("OTP email sent to {Email}", toEmail);
            return true;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to send OTP email to {Email}", toEmail);
            return false;
        }
    }
}
