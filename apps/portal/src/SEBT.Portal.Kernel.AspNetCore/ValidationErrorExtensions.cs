namespace SEBT.Portal.Kernel.AspNetCore;

public static class ValidationErrorExtensions
{
    public static IDictionary<string, string[]> CreateErrorDictionary(this IReadOnlyCollection<ValidationError> validationErrors)
    {
        ArgumentNullException.ThrowIfNull(validationErrors);

        var errorDictionary = new Dictionary<string, string[]>(StringComparer.Ordinal);

        foreach (var (key, message) in validationErrors)
        {
            if (errorDictionary.TryGetValue(key, out var messages))
            {
                // Inefficient, but probably better than allocating a List for each just to convert it back to an array.
                var updatedMessages = new string[messages.Length + 1];
                messages.CopyTo(updatedMessages, 0);
                updatedMessages[^1] = message;
                errorDictionary[key] = updatedMessages;
            }
            else
            {
                errorDictionary.Add(key, [message]);
            }
        }

        return errorDictionary;
    }
}
