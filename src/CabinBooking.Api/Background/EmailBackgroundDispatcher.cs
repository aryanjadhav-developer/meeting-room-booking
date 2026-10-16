using CabinBooking.Application.Abstractions;
using CabinBooking.Infrastructure.Email;

namespace CabinBooking.Api.Background;

public class EmailBackgroundDispatcher(
    EmailOutbox outbox,
    IEmailSender emailSender,
    ILogger<EmailBackgroundDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Email background dispatcher started.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var notification = await outbox.Reader.ReadAsync(stoppingToken);
                if (!string.IsNullOrEmpty(notification.IcsAttachmentContent) && !string.IsNullOrEmpty(notification.IcsAttachmentFileName))
                {
                    await emailSender.SendEmailWithAttachmentAsync(
                        notification.ToEmail,
                        notification.Subject,
                        notification.Body,
                        notification.IcsAttachmentContent,
                        notification.IcsAttachmentFileName,
                        "text/calendar; method=REQUEST; charset=UTF-8",
                        stoppingToken);
                }
                else
                {
                    await emailSender.SendEmailAsync(notification.ToEmail, notification.Subject, notification.Body, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing queued email notification.");
            }
        }
    }
}
