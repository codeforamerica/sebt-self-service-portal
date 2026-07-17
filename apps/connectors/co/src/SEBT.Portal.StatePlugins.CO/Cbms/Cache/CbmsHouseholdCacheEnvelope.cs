namespace SEBT.Portal.StatePlugins.CO.Cbms.Cache;

/// <summary>
/// Cache envelope wrapping the CBMS response as a JSON string rather than the
/// Kiota-generated <c>GetAccountDetailsResponse</c> instance directly. The Kiota
/// type has an <c>IDictionary&lt;string, object&gt; AdditionalData</c> property
/// and IParsable plumbing that doesn't reliably round-trip through HybridCache's
/// default System.Text.Json serializer to L2 (Redis) — observed silent L1-only
/// degradation. Caching a string sidesteps that entirely (matches the pattern
/// used by <c>MockCbmsDataStore</c>) and only adds a microsecond-scale
/// deserialize cost on read.
/// </summary>
internal sealed record CbmsHouseholdCacheEnvelope(
    string ResponseJson,
    DateTimeOffset SoftExpiryUtc,
    DateTimeOffset HardExpiryUtc,
    DateTimeOffset CachedAtUtc);
