using SEBT.Portal.Api.Services;

namespace SEBT.Portal.Tests.Unit.Services;

/// <summary>
/// Unit tests for the state allowlist.
/// Verifies the core guarantees the <see cref="OidcController"/> depends on:
/// empty sets reject everything, lookups are case-insensitive, and whitespace/null
/// inputs cannot spoof a valid state by coincidence.
/// </summary>
public class StateAllowlistTests
{
    [Fact]
    public void Empty_ContainsNothing()
    {
        var allowlist = new StateAllowlist([]);

        Assert.False(allowlist.Contains("co"));
        Assert.False(allowlist.Contains("dc"));
        Assert.Empty(allowlist.All);
    }

    [Fact]
    public void Contains_IsCaseInsensitive()
    {
        var allowlist = new StateAllowlist(["co"]);

        Assert.True(allowlist.Contains("co"));
        Assert.True(allowlist.Contains("CO"));
        Assert.True(allowlist.Contains("Co"));
    }

    [Fact]
    public void Contains_RejectsNullWhitespaceAndUnknown()
    {
        var allowlist = new StateAllowlist(["co"]);

        Assert.False(allowlist.Contains(null));
        Assert.False(allowlist.Contains(""));
        Assert.False(allowlist.Contains("   "));
        Assert.False(allowlist.Contains("nm"));
    }

    [Fact]
    public void Constructor_NormalizesCaseAndDedupes()
    {
        var allowlist = new StateAllowlist(["CO", "co", "Dc", "dc"]);

        Assert.Equal(2, allowlist.All.Count);
        Assert.Contains("co", allowlist.All);
        Assert.Contains("dc", allowlist.All);
    }

    [Fact]
    public void Constructor_FiltersOutBlankEntries()
    {
        var allowlist = new StateAllowlist(["co", "", "   ", null!]);

        Assert.Single(allowlist.All);
        Assert.Contains("co", allowlist.All);
    }
}
