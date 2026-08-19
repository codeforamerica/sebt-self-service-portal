using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.AspNetCore;

namespace SEBT.Portal.Tests.Unit.Kernel.AspNetCore;

public class ValidationErrorExtensionsTests
{
    [Fact]
    public void CreateErrorDictionary_Throws_ForNullCollection()
    {
        IReadOnlyCollection<ValidationError> errors = null!;

        Assert.Throws<ArgumentNullException>(() => errors.CreateErrorDictionary());
    }

    [Fact]
    public void CreateErrorDictionary_ReturnsEmptyDictionary_ForEmptyCollection()
    {
        var dictionary = Array.Empty<ValidationError>().CreateErrorDictionary();

        Assert.Empty(dictionary);
    }

    [Fact]
    public void CreateErrorDictionary_MapsSingleErrorToItsKey()
    {
        var errors = new[] { new ValidationError("Email", "Email is required.") };

        var dictionary = errors.CreateErrorDictionary();

        var (key, messages) = Assert.Single(dictionary);
        Assert.Equal("Email", key);
        Assert.Equal("Email is required.", Assert.Single(messages));
    }

    [Fact]
    public void CreateErrorDictionary_AppendsMessagesInOrder_ForRepeatedKey()
    {
        var errors = new[]
        {
            new ValidationError("Email", "Email is required."),
            new ValidationError("Email", "Email is malformed."),
        };

        var dictionary = errors.CreateErrorDictionary();

        var (_, messages) = Assert.Single(dictionary);
        Assert.Equal(new[] { "Email is required.", "Email is malformed." }, messages);
    }

    [Fact]
    public void CreateErrorDictionary_KeepsDistinctKeysSeparate()
    {
        var errors = new[]
        {
            new ValidationError("Email", "Email is required."),
            new ValidationError("PostalCode", "Postal code is invalid."),
        };

        var dictionary = errors.CreateErrorDictionary();

        Assert.Equal(2, dictionary.Count);
        Assert.Equal("Email is required.", Assert.Single(dictionary["Email"]));
        Assert.Equal("Postal code is invalid.", Assert.Single(dictionary["PostalCode"]));
    }

    [Fact]
    public void CreateErrorDictionary_TreatsKeysCaseSensitively()
    {
        // The dictionary uses ordinal comparison, so differently-cased keys stay separate.
        var errors = new[]
        {
            new ValidationError("email", "lower"),
            new ValidationError("Email", "upper"),
        };

        var dictionary = errors.CreateErrorDictionary();

        Assert.Equal(2, dictionary.Count);
    }
}
