using CabinBooking.Application.Abstractions;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace CabinBooking.Infrastructure.Email;

public record SentEmail(string To, string Subject, string Body, DateTimeOffset SentAt, string? AttachmentFileName = null);

public interface IPreviewEmailStore
{
    IReadOnlyList<SentEmail> GetRecentEmails();
    void Clear();
}

public class EmailSender(IConfiguration configuration, ILogger<EmailSender> logger) : IEmailSender, IPreviewEmailStore
{
    private static readonly List<SentEmail> RecentEmails = new();
    private static readonly object SyncRoot = new();

    public Task SendEmailAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default) =>
        SendEmailInternalAsync(toEmail, subject, body, null, null, null, cancellationToken);

    public Task SendEmailWithAttachmentAsync(
        string toEmail,
        string subject,
        string body,
        string attachmentContent,
        string attachmentFileName,
        string contentType,
        CancellationToken cancellationToken = default) =>
        SendEmailInternalAsync(toEmail, subject, body, attachmentContent, attachmentFileName, contentType, cancellationToken);

    private async Task SendEmailInternalAsync(
        string toEmail,
        string subject,
        string body,
        string? attachmentContent,
        string? attachmentFileName,
        string? contentType,
        CancellationToken cancellationToken)
    {
        var email = new SentEmail(toEmail, subject, body, DateTimeOffset.UtcNow, attachmentFileName);
        lock (SyncRoot)
        {
            RecentEmails.Insert(0, email);
            if (RecentEmails.Count > 50) RecentEmails.RemoveAt(RecentEmails.Count - 1);
        }

        var smtpHost = configuration["Email:SmtpHost"];
        if (string.IsNullOrWhiteSpace(smtpHost))
        {
            logger.LogInformation("No SMTP server configured. Email to {To} previewed in-memory: {Subject}", toEmail, subject);
            return;
        }

        var smtpPort = int.TryParse(configuration["Email:SmtpPort"], out var p) ? p : 1025;
        var fromAddress = configuration["Email:From"] ?? "noreply@meetingrooms.local";

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Meeting Room Booking", fromAddress));
            message.To.Add(new MailboxAddress(toEmail, toEmail));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder { HtmlBody = body };
            if (!string.IsNullOrEmpty(attachmentContent) && !string.IsNullOrEmpty(attachmentFileName))
            {
                var mediaType = ContentType.Parse(contentType ?? "application/octet-stream");
                bodyBuilder.Attachments.Add(attachmentFileName, System.Text.Encoding.UTF8.GetBytes(attachmentContent), mediaType);
            }
            message.Body = bodyBuilder.ToMessageBody();

            var smtpUser = configuration["Email:SmtpUser"] ?? fromAddress;
            var smtpPassword = configuration["Email:SmtpPassword"];

            using var client = new SmtpClient();
            await client.ConnectAsync(smtpHost, smtpPort, MailKit.Security.SecureSocketOptions.StartTls, cancellationToken);
            if (!string.IsNullOrWhiteSpace(smtpPassword))
                await client.AuthenticateAsync(smtpUser, smtpPassword, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            logger.LogInformation("Sent email to {To} via {Host}:{Port}", toEmail, smtpHost, smtpPort);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send email to {To} via SMTP. Stored in preview.", toEmail);
        }
    }

    public IReadOnlyList<SentEmail> GetRecentEmails()
    {
        lock (SyncRoot)
        {
            return RecentEmails.ToList();
        }
    }

    public void Clear()
    {
        lock (SyncRoot)
        {
            RecentEmails.Clear();
        }
    }
}
