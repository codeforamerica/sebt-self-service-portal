using System.Buffers.Text;
using System.Text;
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

    [Fact]
    public void Compose_ThenDecode_RoundTripsUnicodeFieldValues()
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["caseId"] = "CASO-Ñ-123",
            ["householdIdentifier"] = "família@exämple.com — 日本語 🙂",
        };

        var decoded = OpaqueCaseId.Decode(OpaqueCaseId.Compose(fields));

        Assert.Equal(fields, decoded);
    }

    [Fact]
    public void Compose_ThenDecode_RoundTripsMultiKilobyteFieldValue()
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["caseId"] = new string('x', 4096),
        };

        var decoded = OpaqueCaseId.Decode(OpaqueCaseId.Compose(fields));

        Assert.Equal(fields, decoded);
    }

    // Tokens can carry email/phone in householdIdentifier, and write handlers log full
    // exceptions — the raw token must never appear in the failure message.
    [Theory]
    // Not base64 at all.
    [InlineData("not!valid@base64#user@example.com", false, "not valid base64")]
    // Base64, but the payload is not JSON.
    [InlineData("user@example.com is not json", true, "does not decode to routing fields")]
    // The JSON literal "null" deserializes without error to a null dictionary.
    [InlineData("null", true, "decoded to no routing fields")]
    public void Decode_MalformedToken_ThrowsWithoutEchoingToken(
        string payload, bool encodeAsBase64, string expectedMessageFragment)
    {
        string token = encodeAsBase64
            ? Base64Url.EncodeToString(Encoding.UTF8.GetBytes(payload))
            : payload;

        var ex = Assert.Throws<InvalidOperationException>(() => OpaqueCaseId.Decode(token));

        Assert.Contains(expectedMessageFragment, ex.Message);
        Assert.DoesNotContain(token, ex.Message);
    }

    [Fact]
    public void Decode_FailureMessages_AreDistinctPerFailureMode()
    {
        string NotBase64Message()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => OpaqueCaseId.Decode("!!!not-base64!!!"));
            return ex.Message;
        }

        string NotJsonMessage()
        {
            string token = Base64Url.EncodeToString(Encoding.UTF8.GetBytes("plainly not json"));
            var ex = Assert.Throws<InvalidOperationException>(() => OpaqueCaseId.Decode(token));
            return ex.Message;
        }

        string NullJsonMessage()
        {
            string token = Base64Url.EncodeToString(Encoding.UTF8.GetBytes("null"));
            var ex = Assert.Throws<InvalidOperationException>(() => OpaqueCaseId.Decode(token));
            return ex.Message;
        }

        var messages = new[] { NotBase64Message(), NotJsonMessage(), NullJsonMessage() };

        Assert.Equal(3, messages.Distinct(StringComparer.Ordinal).Count());
    }
}
