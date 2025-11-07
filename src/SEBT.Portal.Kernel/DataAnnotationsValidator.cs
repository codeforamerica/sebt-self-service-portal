using System.ComponentModel.DataAnnotations;
using DataAnnotationsValidationResult = System.ComponentModel.DataAnnotations.ValidationResult;
using ValidationResult = SEBT.Portal.Kernel.Results.ValidationResult;

namespace SEBT.Portal.Kernel;

public class DataAnnotationsValidator<T>(IServiceProvider serviceProvider) : IValidator<T>
    where T : notnull
{
    /// <summary>
    /// Validates the specified object.
    /// </summary>
    /// <param name="command">The object to validate.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous validation operation. The task result contains the validation result.</returns>
    public Task<ValidationResult> Validate(T command, CancellationToken cancellationToken = default)
    {
        var context = new ValidationContext(command, serviceProvider, null);
        var results = new List<DataAnnotationsValidationResult>();

        return Validator.TryValidateObject(command, context, results, true)
            ? Task.FromResult(ValidationResult.Passed())
            : Task.FromResult(ValidationResult.Failed(results
                .SelectMany(i => i.MemberNames.Select(n => new { i.ErrorMessage, MemberName = n }))
                .Select(i => new ValidationError(i.MemberName, i.ErrorMessage ?? "Invalid value"))
                .ToList()));
    }
}
