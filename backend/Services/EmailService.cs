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
    Task<bool> SendApplicationApprovedAsync(string toEmail, string fullName, string productName, string? note);
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

    public async Task<bool> SendApplicationApprovedAsync(string toEmail, string fullName, string productName, string? note)
    {
        if (!_smtp.Configured)
        {
            _log.LogWarning("SMTP not configured — approval email for {Email} ({Product}) not sent (dev mode)", toEmail, productName);
            return false;
        }

        var firstName = string.IsNullOrWhiteSpace(fullName) ? "there" : fullName.Trim().Split(' ')[0];
        var dashboardUrl = $"{_smtp.AppBaseUrl.TrimEnd('/')}/applications";
        var extra = string.IsNullOrWhiteSpace(note)
            ? ""
            : $"<p style='color:#4A483F;margin:0 0 14px'><strong>A note from our team:</strong> {System.Net.WebUtility.HtmlEncode(note)}</p>";

        var html = $@"
            <div style='font-family:Inter,Arial,sans-serif;max-width:520px;margin:auto;border:1px solid #ECE8DE;border-radius:14px;overflow:hidden'>
              <div style='background:linear-gradient(135deg,#C4603C,#A24A2C);padding:28px 32px'>
                <div style='color:#FFF;font-size:13px;letter-spacing:2px;opacity:.85'>VERIQO BANK</div>
                <div style='color:#FFF;font-size:24px;font-weight:700;margin-top:6px'>Your application is approved 🎉</div>
              </div>
              <div style='padding:28px 32px'>
                <p style='color:#262520;font-size:16px;margin:0 0 14px'>Hi {System.Net.WebUtility.HtmlEncode(firstName)},</p>
                <p style='color:#4A483F;margin:0 0 14px'>Great news — your <strong>{System.Net.WebUtility.HtmlEncode(productName)}</strong> application has been reviewed and <strong style='color:#2E7D4F'>approved</strong>. Welcome aboard!</p>
                {extra}
                <p style='color:#4A483F;margin:0 0 22px'>You can view the full details and next steps from your dashboard.</p>
                <a href='{dashboardUrl}' style='display:inline-block;background:#C4603C;color:#FFF;text-decoration:none;font-weight:600;padding:12px 22px;border-radius:9px'>View your application</a>
                <p style='color:#77756B;font-size:13px;margin:26px 0 0'>Thank you for choosing Veriqo Bank. If you have any questions, just reply to this email or raise a support ticket from your dashboard.</p>
              </div>
            </div>";

        var text = $"Hi {firstName},\n\nGreat news — your {productName} application has been approved. Welcome aboard!"
            + (string.IsNullOrWhiteSpace(note) ? "" : $"\n\nA note from our team: {note}")
            + $"\n\nView your application: {dashboardUrl}\n\nThank you for choosing Veriqo Bank.";

        return await SendAsync(toEmail, $"Your {productName} application is approved", html, text);
    }

    /// <summary>Shared SMTP send with the same dev-safe TLS handling as the OTP path.</summary>
    private async Task<bool> SendAsync(string toEmail, string subject, string htmlBody, string textBody)
    {
        try
        {
            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress(_smtp.FromName, _smtp.FromEmail));
            msg.To.Add(MailboxAddress.Parse(toEmail));
            msg.Subject = subject;
            msg.Body = new BodyBuilder { HtmlBody = htmlBody, TextBody = textBody }.ToMessageBody();

            using var client = new SmtpClient();
            client.CheckCertificateRevocation = false;
            await client.ConnectAsync(_smtp.Host, _smtp.Port,
                _smtp.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.SslOnConnect);
            await client.AuthenticateAsync(_smtp.User, _smtp.Password);
            await client.SendAsync(msg);
            await client.DisconnectAsync(true);
            _log.LogInformation("Email '{Subject}' sent to {Email}", subject, toEmail);
            return true;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to send '{Subject}' email to {Email}", subject, toEmail);
            return false;
        }
    }
}
