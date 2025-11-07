using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace SEBT.Portal.Kernel.AspNetCore;

/// <summary>
/// Provides extension methods for working with the <see cref="Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary"/> class
/// to map <see cref="ValidationError"/> to the appropriate structure for HTTP response.
/// </summary>
public static class ModelStateExtensions
{
    /// <summary>
    /// Adds multiple validation errors to the <see cref="ModelStateDictionary"/> for a specified key.
    /// </summary>
    /// <param name="modelState">The <see cref="ModelStateDictionary"/> instance where errors will be added.</param>
    /// <param name="key">The key to associate with each of the validation errors.</param>
    /// <param name="errors">The collection of <see cref="ValidationError"/> objects to add to the model state.</param>
    public static void AddModelErrors(this ModelStateDictionary modelState, string key,
        IEnumerable<ValidationError> errors)
    {
        foreach (var error in errors)
        {
            modelState.AddModelError(key, error.Message);
        }
    }

    /// <summary>
    /// Adds multiple validation errors to the <see cref="ModelStateDictionary"/> using the keys specified by each error.
    /// </summary>
    /// <param name="modelState">The <see cref="ModelStateDictionary"/> instance where errors will be added.</param>
    /// <param name="errors">The collection of <see cref="ValidationError"/> objects to add to the model state.</param>
    public static void AddModelErrors(this ModelStateDictionary modelState, IEnumerable<ValidationError> errors)
    {
        foreach (var error in errors)
        {
            modelState.AddModelError(error.Key, error.Message);
        }
    }

    /// <summary>
    /// Converts a collection of <see cref="ValidationError"/> objects to a populated <see cref="ModelStateDictionary"/>.
    /// </summary>
    /// <param name="errors">The collection of <see cref="ValidationError"/> objects to add to the <see cref="ModelStateDictionary"/>.</param>
    /// <returns>A <see cref="ModelStateDictionary"/> instance containing the specified validation errors.</returns>
    public static ModelStateDictionary ToModelState(this IEnumerable<ValidationError> errors)
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelErrors(errors);
        return modelState;
    }
}