using SEBT.Portal.StatesPlugins.Interfaces.Models.Household;
using Xunit;

namespace SEBT.Portal.StatesPlugins.Interfaces.Tests;

public class CardReplacementResultTests
{
    [Fact]
    public void Success_sets_IsSuccess_true_and_clears_rejection_flags()
    {
        var result = CardReplacementResult.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsPolicyRejection);
        Assert.Null(result.ErrorCode);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void PolicyRejected_sets_IsPolicyRejection_and_preserves_code_and_message()
    {
        var result = CardReplacementResult.PolicyRejected("INELIGIBLE", "Not allowed right now.");

        Assert.False(result.IsSuccess);
        Assert.True(result.IsPolicyRejection);
        Assert.Equal("INELIGIBLE", result.ErrorCode);
        Assert.Equal("Not allowed right now.", result.ErrorMessage);
    }

    [Fact]
    public void BackendError_sets_both_failure_flags_false_and_preserves_code_and_message()
    {
        var result = CardReplacementResult.BackendError("UPSTREAM_500", "Something broke downstream.");

        Assert.False(result.IsSuccess);
        Assert.False(result.IsPolicyRejection);
        Assert.Equal("UPSTREAM_500", result.ErrorCode);
        Assert.Equal("Something broke downstream.", result.ErrorMessage);
    }
}
