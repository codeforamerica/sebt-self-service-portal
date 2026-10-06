using Microsoft.Extensions.Options;
using SEBT.Portal.Api.Models.Household;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Tests.Unit.Api.Models.Household;

public class HouseholdAnalyticsIdentifiersTests
{
    private static readonly IIdentifierHasher Hasher = new IdentifierHasher(
        Options.Create(new IdentifierHasherSettings { SecretKey = "TestKeyMustBeAtLeast32CharactersLong!!" }));

    [Fact]
    public void Resolve_WhenApplicationsExist_HashesLowestApplicationNumberEvenIfACaseIdIsLower()
    {
        var household = new HouseholdData
        {
            Applications = new List<Application>
            {
                new Application { ApplicationNumber = "1199189" },
                new Application { ApplicationNumber = "1199119" }
            },
            SummerEbtCases = new List<SummerEbtCase> { new SummerEbtCase { SourceApplicationId = "1192789" } }
        };

        var identifiers = HouseholdAnalyticsIdentifiers.Resolve(household, Hasher);

        Assert.Equal(Hasher.HashForAnalytics("1199119"), identifiers.HashedAppId);
    }

    [Fact]
    public void Resolve_WhenNoChildApplied_FallsBackToLowestCaseApplicationId()
    {
        var household = new HouseholdData
        {
            SummerEbtCases = new List<SummerEbtCase>
            {
                new SummerEbtCase { SourceApplicationId = "1199181", SourceChildId = "1200736" },
                new SummerEbtCase { SourceApplicationId = "1199180", SourceChildId = "1200732" }
            }
        };

        var identifiers = HouseholdAnalyticsIdentifiers.Resolve(household, Hasher);

        Assert.Equal(Hasher.HashForAnalytics("1199180"), identifiers.HashedAppId);
        Assert.Equal(DigestPrefixes("1199181", "1199180"), identifiers.HashedAppIds);
        Assert.Equal(DigestPrefixes("1200736", "1200732"), identifiers.HashedCaseIds);
    }

    [Fact]
    public void Resolve_ListsEveryApplicationAndChildOnce_IncludingPendingApplicants()
    {
        var household = new HouseholdData
        {
            Applications = new List<Application>
            {
                new Application
                {
                    ApplicationNumber = "1199119",
                    Children = new List<Child> { new Child { FirstName = "Polly", SourceChildId = "1200686" } }
                },
                new Application
                {
                    ApplicationNumber = "111682",
                    Children = new List<Child>
                    {
                        new Child { FirstName = "Adelaide", SourceChildId = "125726" },
                        new Child { FirstName = "Anthony", SourceChildId = "1203589" }
                    }
                }
            },
            SummerEbtCases = new List<SummerEbtCase>
            {
                new SummerEbtCase { ChildFirstName = "Dolly", SourceApplicationId = "1192789", SourceChildId = "1233721" },
                new SummerEbtCase { ChildFirstName = "Adelaide", SourceApplicationId = "111682", SourceChildId = "125726" },
                new SummerEbtCase { ChildFirstName = "Anthony", SourceApplicationId = "111682", SourceChildId = "1203589" }
            }
        };

        var identifiers = HouseholdAnalyticsIdentifiers.Resolve(household, Hasher);

        Assert.Equal(DigestPrefixes("1199119", "111682", "1192789"), identifiers.HashedAppIds);
        Assert.Equal(DigestPrefixes("1200686", "125726", "1203589", "1233721"), identifiers.HashedCaseIds);
        Assert.All(identifiers.HashedCaseIds!.Split(','), prefix => Assert.Matches("^[0-9a-f]{16}$", prefix));
    }

    [Fact]
    public void Resolve_WhenNoIdIsKnown_ReturnsNulls()
    {
        var household = new HouseholdData
        {
            Applications = new List<Application>
            {
                new Application { ApplicationNumber = " ", Children = new List<Child> { new Child() } }
            },
            SummerEbtCases = new List<SummerEbtCase> { new SummerEbtCase { SourceApplicationId = "", SourceChildId = null } }
        };

        var identifiers = HouseholdAnalyticsIdentifiers.Resolve(household, Hasher);

        Assert.Equal(new HouseholdAnalyticsIdentifiers(null, null, null), identifiers);
    }

    private static string DigestPrefixes(params string[] ids) =>
        string.Join(',', ids.Select(id => Hasher.HashForAnalytics(id)![..16]).Order(StringComparer.Ordinal));
}
