namespace CabinBooking.Application.Common;

public abstract class AppException(string message) : Exception(message);

public sealed class AppValidationException(string message) : AppException(message);

public sealed class NotFoundException(string message) : AppException(message);

public sealed class ForbiddenException(string message = "You are not allowed to perform this action.") : AppException(message);

public sealed class ConflictException(string message) : AppException(message);
