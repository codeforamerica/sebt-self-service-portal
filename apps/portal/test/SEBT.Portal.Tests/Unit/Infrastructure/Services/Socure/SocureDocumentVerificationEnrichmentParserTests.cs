using System.Text.Json;
using SEBT.Portal.Infrastructure.Services.Socure;

namespace SEBT.Portal.Tests.Unit.Infrastructure.Services.Socure;

public class SocureDocumentVerificationEnrichmentParserTests
{
    private const string DocvProvider = "SocureDocRequest";

    [Fact]
    public void TryGetEnrichmentResponse_PrefersConfiguredProvider()
    {
        var other = JsonDocument.Parse(
            """{"documentVerification":{"reasonCodes":["R999"]}}""").RootElement;
        var preferred = JsonDocument.Parse(
            """{"documentVerification":{"reasonCodes":["R815"]}}""").RootElement;

        var enrichments = new[]
        {
            new SocureEnrichmentResponseRef("OtherProvider", other),
            new SocureEnrichmentResponseRef(DocvProvider, preferred),
        };

        var result = SocureDocumentVerificationEnrichmentParser.TryGetEnrichmentResponse(
            enrichments, DocvProvider);

        Assert.NotNull(result);
        var codes = SocureDocumentVerificationEnrichmentParser.ExtractReasonCodes(result.Value);
        Assert.Equal(["R815"], codes);
    }

    [Fact]
    public void TryGetEnrichmentResponse_FallsBackToDocumentVerificationEnrichment()
    {
        var fallback = JsonDocument.Parse(
            """{"documentVerification":{"reasonCodes":["R836"]}}""").RootElement;

        var enrichments = new[]
        {
            new SocureEnrichmentResponseRef("OtherProvider", fallback),
        };

        var result = SocureDocumentVerificationEnrichmentParser.TryGetEnrichmentResponse(
            enrichments, DocvProvider);

        Assert.NotNull(result);
        Assert.Equal(["R836"], SocureDocumentVerificationEnrichmentParser.ExtractReasonCodes(result.Value));
    }

    [Fact]
    public void ExtractReasonCodes_ReturnsEmpty_WhenPropertyMissing()
    {
        var response = JsonDocument.Parse("""{"data":{"url":"https://example.com"}}""").RootElement;

        Assert.Empty(SocureDocumentVerificationEnrichmentParser.ExtractReasonCodes(response));
    }
}
