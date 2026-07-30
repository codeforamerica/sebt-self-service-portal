using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.FeatureManagement;
using NSubstitute;
using SEBT.Portal.Api.Controllers.EnrollmentCheck;
using SEBT.Portal.Api.Models.EnrollmentCheck;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.StateBackendAdapters;
using SEBT.Portal.StatesPlugins.Interfaces;
using SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck;
using SEBT.Portal.UseCases.EnrollmentCheck;

namespace SEBT.Portal.Tests.Unit.UseCases.EnrollmentCheck;

/// <summary>
/// Pins the EXACT serialized enrollment-check wire response across the composed
/// portal path — controller, handler, the real plugin adapter, and response
/// mapping — over a mocked contract <see cref="IEnrollmentCheckService"/>. Every
/// assertion captures the response byte-for-byte (ASP.NET web-default JSON:
/// camelCase, nulls included), so any internal re-plumbing of the enrollment path
/// must keep these green without touching the expected strings.
/// </summary>
public class EnrollmentCheckPluginCompositionTests
{
    private readonly IEnrollmentCheckService _contractService =
        Substitute.For<IEnrollmentCheckService>();
    private readonly IEnrollmentCheckSubmissionLogger _submissionLogger =
        Substitute.For<IEnrollmentCheckSubmissionLogger>();
    private readonly IFeatureManager _featureManager = Substitute.For<IFeatureManager>();
    private IConfiguration _configuration = new ConfigurationBuilder().Build();

    /// <summary>Serializer matching ASP.NET Core's wire defaults for controllers.</summary>
    private static readonly JsonSerializerOptions WireJson = JsonSerializerOptions.Web;

    /// <summary>
    /// Sets the exact-match flag at both of its read sites: the handler's IFeatureManager
    /// (synthetic NonMatch insertion) and the adapter's configuration read (the guard).
    /// </summary>
    private void SetExactMatchFilterFlag(bool enabled)
    {
        _featureManager
            .IsEnabledAsync(FeatureFlags.EnrollmentCheckRequiresAtLeastOneExactMatchedField)
            .Returns(enabled);
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"FeatureManagement:{FeatureFlags.EnrollmentCheckRequiresAtLeastOneExactMatchedField}"] =
                    enabled.ToString()
            })
            .Build();
    }

    /// <summary>Handler wired to the REAL adapter over the mocked contract service.</summary>
    private CheckEnrollmentCommandHandler CreateHandler() =>
        new(new PluginEnrollmentCheckBackend(
                _contractService, _configuration, NullLogger<PluginEnrollmentCheckBackend>.Instance),
            _submissionLogger,
            NullLogger<CheckEnrollmentCommandHandler>.Instance, _featureManager);

    private static EnrollmentCheckController CreateController() =>
        new()
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    private static EnrollmentCheckApiRequest CreateRequest(params (string First, string Last, string Dob)[] children) =>
        new()
        {
            Children = children
                .Select(c => new ChildCheckApiRequest
                {
                    FirstName = c.First,
                    LastName = c.Last,
                    DateOfBirth = c.Dob
                })
                .ToList()
        };

    /// <summary>Posts the request through the real controller + handler and returns the wire JSON.</summary>
    private async Task<string> PostAndSerializeAsync(EnrollmentCheckApiRequest request)
    {
        var result = await CreateController().CheckEnrollment(CreateHandler(), request);
        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<EnrollmentCheckApiResponse>(ok.Value);
        return JsonSerializer.Serialize(response, WireJson);
    }

    /// <summary>
    /// Stubs the contract service with one connector result per submitted child (by index),
    /// capturing the handler-generated CheckIds so expected JSON can reference them.
    /// A null builder entry omits that child from the connector response.
    /// </summary>
    private List<Guid> StubConnector(
        string? responseMessage,
        params Func<Guid, ChildCheckResult>?[] resultBuilders)
    {
        var capturedCheckIds = new List<Guid>();
        _contractService
            .CheckEnrollmentAsync(Arg.Any<EnrollmentCheckRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<EnrollmentCheckRequest>();
                capturedCheckIds.Clear();
                capturedCheckIds.AddRange(request.Children.Select(c => c.CheckId));

                var results = new List<ChildCheckResult>();
                for (var i = 0; i < request.Children.Count; i++)
                {
                    if (resultBuilders[i] is { } build)
                    {
                        results.Add(build(request.Children[i].CheckId));
                    }
                }

                return new EnrollmentCheckResult
                {
                    Results = results,
                    ResponseMessage = responseMessage
                };
            });
        return capturedCheckIds;
    }

    [Fact]
    public async Task Match_WithConfidenceStatusMessageAndResultMessage_SerializesExactly()
    {
        // CO-shaped: confidence + per-child status text + result-level message, with
        // connector-normalized identity casing that must NOT reach the wire.
        SetExactMatchFilterFlag(false);
        var checkIds = StubConnector(
            "Success",
            checkId => new ChildCheckResult
            {
                CheckId = checkId,
                FirstName = "JANE",
                LastName = "DOE",
                DateOfBirth = new DateOnly(2015, 3, 12),
                Status = EnrollmentStatus.Match,
                MatchConfidence = 97.5,
                StatusMessage = "SEBT ELIGIBLE"
            });

        var json = await PostAndSerializeAsync(CreateRequest(("Jane", "Doe", "2015-03-12")));

        Assert.Equal(
            $"{{\"results\":[{{\"checkId\":\"{checkIds[0]}\",\"firstName\":\"Jane\",\"lastName\":\"Doe\"," +
            "\"dateOfBirth\":\"2015-03-12\",\"status\":\"Match\",\"matchConfidence\":97.5," +
            "\"eligibilityType\":null,\"schoolName\":null,\"statusMessage\":\"SEBT ELIGIBLE\"}]," +
            "\"message\":\"Success\"}",
            json);
    }

    [Fact]
    public async Task NonMatch_WithNoOptionalFields_SerializesExactly()
    {
        // DC-shaped: bare NonMatch, no confidence/status text, no result-level message.
        SetExactMatchFilterFlag(false);
        var checkIds = StubConnector(
            null,
            checkId => new ChildCheckResult
            {
                CheckId = checkId,
                FirstName = "Jane",
                LastName = "Doe",
                DateOfBirth = new DateOnly(2015, 3, 12),
                Status = EnrollmentStatus.NonMatch
            });

        var json = await PostAndSerializeAsync(CreateRequest(("Jane", "Doe", "2015-03-12")));

        Assert.Equal(
            $"{{\"results\":[{{\"checkId\":\"{checkIds[0]}\",\"firstName\":\"Jane\",\"lastName\":\"Doe\"," +
            "\"dateOfBirth\":\"2015-03-12\",\"status\":\"NonMatch\",\"matchConfidence\":null," +
            "\"eligibilityType\":null,\"schoolName\":null,\"statusMessage\":null}]," +
            "\"message\":null}",
            json);
    }

    [Fact]
    public async Task FilterFlagOn_ConnectorOmitsChild_InsertsSyntheticNonMatch()
    {
        // The connector returned no result for the child; the response still carries
        // one entry per submitted child — a synthetic NonMatch with submitted identity.
        SetExactMatchFilterFlag(true);
        var checkIds = StubConnector(null, new Func<Guid, ChildCheckResult>?[] { null });

        var json = await PostAndSerializeAsync(CreateRequest(("Jane", "Doe", "2015-03-12")));

        Assert.Equal(
            $"{{\"results\":[{{\"checkId\":\"{checkIds[0]}\",\"firstName\":\"Jane\",\"lastName\":\"Doe\"," +
            "\"dateOfBirth\":\"2015-03-12\",\"status\":\"NonMatch\",\"matchConfidence\":null," +
            "\"eligibilityType\":null,\"schoolName\":null,\"statusMessage\":null}]," +
            "\"message\":null}",
            json);
    }

    [Fact]
    public async Task FilterFlagOff_ConnectorOmitsChild_ResponseStaysEmpty()
    {
        // Without the exact-match filter, no synthetic results are inserted: an
        // omitted child simply has no entry in the response.
        SetExactMatchFilterFlag(false);
        StubConnector("Processed", new Func<Guid, ChildCheckResult>?[] { null });

        var json = await PostAndSerializeAsync(CreateRequest(("Jane", "Doe", "2015-03-12")));

        Assert.Equal("{\"results\":[],\"message\":\"Processed\"}", json);
    }

    [Fact]
    public async Task FilterFlagOn_FuzzyMatchWithNoExactField_DowngradesToBareNonMatch()
    {
        // The connector fuzzy-matched a candidate whose identity exact-matches on NO
        // field (same birth year, different full DOB, different name). The filter
        // drops it, and the wire shows a bare NonMatch: submitted identity, with the
        // connector's confidence and status text scrubbed.
        SetExactMatchFilterFlag(true);
        var checkIds = StubConnector(
            null,
            checkId => new ChildCheckResult
            {
                CheckId = checkId,
                FirstName = "Roberta",
                LastName = "Smith",
                DateOfBirth = new DateOnly(2015, 6, 1),
                Status = EnrollmentStatus.Match,
                MatchConfidence = 88.0,
                StatusMessage = "POSSIBLE MATCH"
            });

        var json = await PostAndSerializeAsync(CreateRequest(("Jane", "Doe", "2015-03-12")));

        Assert.Equal(
            $"{{\"results\":[{{\"checkId\":\"{checkIds[0]}\",\"firstName\":\"Jane\",\"lastName\":\"Doe\"," +
            "\"dateOfBirth\":\"2015-03-12\",\"status\":\"NonMatch\",\"matchConfidence\":null," +
            "\"eligibilityType\":null,\"schoolName\":null,\"statusMessage\":null}]," +
            "\"message\":null}",
            json);
    }

    [Fact]
    public async Task FilterFlagOn_MixedBatch_SurvivorsPrecedeSyntheticNonMatches()
    {
        // Two children: the first is a fuzzy match the filter drops, the second an
        // exact match that survives. The wire order is pinned: surviving results
        // first (connector order), synthetic NonMatches appended after.
        SetExactMatchFilterFlag(true);
        var checkIds = StubConnector(
            null,
            checkId => new ChildCheckResult
            {
                CheckId = checkId,
                FirstName = "Roberta",
                LastName = "Smith",
                DateOfBirth = new DateOnly(2015, 6, 1),
                Status = EnrollmentStatus.Match,
                MatchConfidence = 88.0,
                StatusMessage = "POSSIBLE MATCH"
            },
            checkId => new ChildCheckResult
            {
                CheckId = checkId,
                FirstName = "LUIS",
                LastName = "GARCIA",
                DateOfBirth = new DateOnly(2017, 7, 24),
                Status = EnrollmentStatus.Match,
                MatchConfidence = 99.0,
                StatusMessage = "SEBT ELIGIBLE"
            });

        var json = await PostAndSerializeAsync(CreateRequest(
            ("Jane", "Doe", "2015-03-12"),
            ("Luis", "Garcia", "2017-07-24")));

        Assert.Equal(
            $"{{\"results\":[{{\"checkId\":\"{checkIds[1]}\",\"firstName\":\"Luis\",\"lastName\":\"Garcia\"," +
            "\"dateOfBirth\":\"2017-07-24\",\"status\":\"Match\",\"matchConfidence\":99," +
            "\"eligibilityType\":null,\"schoolName\":null,\"statusMessage\":\"SEBT ELIGIBLE\"}," +
            $"{{\"checkId\":\"{checkIds[0]}\",\"firstName\":\"Jane\",\"lastName\":\"Doe\"," +
            "\"dateOfBirth\":\"2015-03-12\",\"status\":\"NonMatch\",\"matchConfidence\":null," +
            "\"eligibilityType\":null,\"schoolName\":null,\"statusMessage\":null}]," +
            "\"message\":null}",
            json);
    }

    [Fact]
    public async Task SchoolIdentifier_FansOutToBothContractSchoolFields_SchoolCodeWinsCoalesce()
    {
        // The submitted schoolCode (over schoolName) becomes the single school identifier,
        // fanned out to BOTH contract fields: DC reads SchoolName, CO reads SchoolCode.
        SetExactMatchFilterFlag(false);
        EnrollmentCheckRequest? seenRequest = null;
        _contractService
            .CheckEnrollmentAsync(Arg.Do<EnrollmentCheckRequest>(r => seenRequest = r), Arg.Any<CancellationToken>())
            .Returns(new EnrollmentCheckResult { Results = [] });

        var request = new EnrollmentCheckApiRequest
        {
            Children =
            [
                new ChildCheckApiRequest
                {
                    FirstName = "Jane",
                    LastName = "Doe",
                    DateOfBirth = "2015-03-12",
                    SchoolName = "Lincoln Elementary",
                    SchoolCode = "SCH-042"
                }
            ]
        };

        await PostAndSerializeAsync(request);

        var child = Assert.Single(Assert.IsAssignableFrom<EnrollmentCheckRequest>(seenRequest).Children);
        Assert.Equal("SCH-042", child.SchoolName);
        Assert.Equal("SCH-042", child.SchoolCode);
    }

    [Fact]
    public async Task ContractRequest_CarriesNoAdditionalFields()
    {
        // No connector reads AdditionalFields; the composed path leaves it empty.
        SetExactMatchFilterFlag(false);
        EnrollmentCheckRequest? seenRequest = null;
        _contractService
            .CheckEnrollmentAsync(Arg.Do<EnrollmentCheckRequest>(r => seenRequest = r), Arg.Any<CancellationToken>())
            .Returns(new EnrollmentCheckResult { Results = [] });

        await PostAndSerializeAsync(CreateRequest(("Jane", "Doe", "2015-03-12")));

        var child = Assert.Single(Assert.IsAssignableFrom<EnrollmentCheckRequest>(seenRequest).Children);
        Assert.Empty(child.AdditionalFields);
    }
}
