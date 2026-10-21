using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CabinBooking.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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
    private static readonly HttpClient Http = new();

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

        var brevoApiKey = configuration["Brevo:ApiKey"];
        if (!string.IsNullOrWhiteSpace(brevoApiKey))
        {
            await SendViaBrevoAsync(brevoApiKey, toEmail, subject, body, cancellationToken);
            return;
        }

        logger.LogInformation("No email provider configured. Email to {To} previewed in-memory: {Subject}", toEmail, subject);
    }

    private async Task SendViaBrevoAsync(string apiKey, string toEmail, string subject, string body, CancellationToken cancellationToken)
    {
        var fromAddress = configuration["Email:From"] ?? "onboarding@brevo.com";
        var fromName = "Meeting Room Booking";
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                sender = new { name = fromName, email = fromAddress },
                to = new[] { new { email = toEmail } },
                subject,
                htmlContent = body
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
            request.Headers.Add("api-key", apiKey);
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            using var response = await Http.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
                logger.LogInformation("Sent email to {To} via Brevo", toEmail);
            else
                logger.LogWarning("Failed to send email to {To} via Brevo: {Status} {Body}", toEmail, (int)response.StatusCode, responseBody);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Failed to send email to {To} via Brevo: {Error}", toEmail, ex.ToString());
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
