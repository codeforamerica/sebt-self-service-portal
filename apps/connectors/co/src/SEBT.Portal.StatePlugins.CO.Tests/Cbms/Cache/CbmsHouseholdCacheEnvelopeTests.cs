using SEBT.Portal.StatePlugins.CO.Cbms.Cache;

namespace SEBT.Portal.StatePlugins.CO.Tests.Cbms.Cache;

public class CbmsHouseholdCacheEnvelopeTests
{
    [Fact]
    public void Envelope_carries_response_json_and_expiries()
    {
        const string json = "{\"RespCd\":\"00\"}";
        var now = DateTimeOffset.UtcNow;
        var envelope = new CbmsHouseholdCacheEnvelope(
            ResponseJson: json,
            SoftExpiryUtc: now.AddMinutes(15),
            HardExpiryUtc: now.AddHours(4),
            CachedAtUtc: now);

        Assert.Equal(json, envelope.ResponseJson);
        Assert.Equal(now.AddMinutes(15), envelope.SoftExpiryUtc);
        Assert.Equal(now.AddHours(4), envelope.HardExpiryUtc);
        Assert.Equal(now, envelope.CachedAtUtc);
    }

    [Fact]
    public void Envelope_is_record_with_value_equality()
    {
        const string json = "{\"RespCd\":\"00\"}";
        var now = DateTimeOffset.UtcNow;
        var a = new CbmsHouseholdCacheEnvelope(json, now.AddMinutes(15), now.AddHours(4), now);
        var b = new CbmsHouseholdCacheEnvelope(json, now.AddMinutes(15), now.AddHours(4), now);

        Assert.Equal(a, b);
    }
}
