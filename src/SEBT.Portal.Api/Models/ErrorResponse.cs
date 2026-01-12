using SEBT.Portal.Kernel;

namespace SEBT.Portal.Api.Models;

/// <summary>
/// Standard error response model for API error responses.
/// </summary>
/// <param name="Error">The error message.</param>
/// <param name="Errors">Optional collection of validation errors, if applicable.</param>
public record ErrorResponse(string Error, IReadOnlyCollection<ValidationError>? Errors = null);

