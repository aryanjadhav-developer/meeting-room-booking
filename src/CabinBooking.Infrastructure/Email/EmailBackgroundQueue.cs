using System.Threading.Channels;
using CabinBooking.Application.Abstractions;

namespace CabinBooking.Infrastructure.Email;

public class EmailOutbox : IEmailOutbox
{
    private readonly Channel<EmailNotification> _channel = Channel.CreateUnbounded<EmailNotification>(new UnboundedChannelOptions
    {
        SingleReader = true
    });

    public ValueTask QueueEmailAsync(EmailNotification notification) =>
        _channel.Writer.WriteAsync(notification);

    public ChannelReader<EmailNotification> Reader => _channel.Reader;
}
