using CabinBooking.Application.Dtos;
using FluentValidation;

namespace CabinBooking.Application.Validation;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(256).WithMessage("Email must not exceed 256 characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.");
    }
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}

public class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");
    }
}

public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Reset token is required.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.");
    }
}

public class UpsertCabinRequestValidator : AbstractValidator<UpsertCabinRequest>
{
    public UpsertCabinRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Room/Cabin name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");

        RuleFor(x => x.Capacity)
            .GreaterThan(0).WithMessage("Capacity must be at least 1 person.")
            .LessThanOrEqualTo(500).WithMessage("Capacity cannot exceed 500.");

        RuleFor(x => x.Color)
            .NotEmpty().WithMessage("Color code is required.")
            .Matches(@"^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$")
            .WithMessage("Color must be a valid hex color code (e.g., #3b82f6).");
    }
}

public class CreateBookingRequestValidator : AbstractValidator<CreateBookingRequest>
{
    public CreateBookingRequestValidator()
    {
        RuleFor(x => x.CabinId)
            .GreaterThan(0).WithMessage("A valid cabin must be selected.");

        RuleFor(x => x.Title)
            .Must(t => !string.IsNullOrWhiteSpace(t)).WithMessage("Project is required.")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters.");

        RuleFor(x => x.Purpose)
            .NotNull().WithMessage("Purpose is required.");

        RuleFor(x => x.StartTime)
            .Must((req, start) => start < req.EndTime)
            .WithMessage("Start time must be before end time.");

        RuleFor(x => x)
            .Must(req => (req.EndTime - req.StartTime).TotalMinutes >= 15)
            .WithMessage("Booking duration must be at least 15 minutes.")
            .Must(req => (req.EndTime - req.StartTime).TotalHours <= 24)
            .WithMessage("Booking duration cannot exceed 24 hours.");
    }
}

public class UpdateBookingRequestValidator : AbstractValidator<UpdateBookingRequest>
{
    public UpdateBookingRequestValidator()
    {
        RuleFor(x => x.CabinId)
            .GreaterThan(0).WithMessage("A valid cabin must be selected.");

        RuleFor(x => x.Title)
            .Must(t => !string.IsNullOrWhiteSpace(t)).WithMessage("Project is required.")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters.");

        RuleFor(x => x.Purpose)
            .NotNull().WithMessage("Purpose is required.");

        RuleFor(x => x.StartTime)
            .Must((req, start) => start < req.EndTime)
            .WithMessage("Start time must be before end time.");

        RuleFor(x => x)
            .Must(req => (req.EndTime - req.StartTime).TotalMinutes >= 15)
            .WithMessage("Booking duration must be at least 15 minutes.")
            .Must(req => (req.EndTime - req.StartTime).TotalHours <= 24)
            .WithMessage("Booking duration cannot exceed 24 hours.");
    }
}

public class QuickBookRequestValidator : AbstractValidator<QuickBookRequest>
{
    public QuickBookRequestValidator()
    {
        RuleFor(x => x.CabinId)
            .GreaterThan(0).WithMessage("A valid cabin must be selected.");

        RuleFor(x => x.DurationMinutes)
            .InclusiveBetween(15, 240).WithMessage("Quick-book duration must be between 15 and 240 minutes.");
    }
}

public class UpdateSettingsRequestValidator : AbstractValidator<UpdateSettingsRequest>
{
    public UpdateSettingsRequestValidator()
    {
        RuleFor(x => x.OfficeStart)
            .Must((req, start) => start < req.OfficeEnd)
            .WithMessage("OfficeStart must be before OfficeEnd.");

        RuleFor(x => x.SlotMinutes)
            .InclusiveBetween(5, 120).WithMessage("SlotMinutes must be between 5 and 120 minutes.");

        RuleFor(x => x.MinDurationMinutes)
            .InclusiveBetween(5, 480).WithMessage("MinDurationMinutes must be between 5 and 480 minutes.");

        RuleFor(x => x.MaxDurationMinutes)
            .InclusiveBetween(15, 1440).WithMessage("MaxDurationMinutes must be between 15 and 1440 minutes.");

        RuleFor(x => x.MaxDaysAhead)
            .InclusiveBetween(1, 365).WithMessage("MaxDaysAhead must be between 1 and 365 days.");

        RuleFor(x => x.WorkingDays)
            .NotEmpty().WithMessage("At least one working day must be specified.");
    }
}
