namespace CabinBooking.Application.Options;

public class AdminOptions
{
    public const string SectionName = "Admin";

    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string Name { get; set; } = "Administrator";
}
