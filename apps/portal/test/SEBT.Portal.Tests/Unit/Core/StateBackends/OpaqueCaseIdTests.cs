using SEBT.Portal.Core.StateBackends;

namespace SEBT.Portal.Tests.Unit.Core.StateBackends;

public class OpaqueCaseIdTests
{
    [Fact]
    public void Compose_SameFieldsInSameOrder_YieldsByteIdenticalTokens()
    {
        // Callers use the token as a merge/lookup key across separate fetches,
        // so composition must be deterministic for a fixed insertion order.
        static Dictionary<string, string> BuildFields() => new(StringComparer.Ordinal)
        {
            ["caseId"] = "STATE-CASE-123",
            ["applicationId"] = "APP-9",
            ["applicationStudentId"] = "STU-7",
            ["householdIdentifier"] = "user@example.com",
        };

        var first = OpaqueCaseId.Compose(BuildFields());
        var second = OpaqueCaseId.Compose(BuildFields());

        Assert.Equal(first, second);
    }

    [Fact]
    public void Compose_ThenDecode_RoundTripsAllFields()
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["caseId"] = "STATE-CASE-123",
            ["householdIdentifier"] = "user@example.com",
        };

        var decoded = OpaqueCaseId.Decode(OpaqueCaseId.Compose(fields));

        Assert.Equal(fields, decoded);
    }
}
