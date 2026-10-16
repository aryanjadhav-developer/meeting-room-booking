namespace CabinBooking.Application.Options;

public class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>When not empty, only e-mails on these domains may register (e.g. "example.com").</summary>
    public string[] AllowedEmailDomains { get; set; } = [];
}
