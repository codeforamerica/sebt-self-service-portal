using Bogus;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Utilities;

namespace SEBT.Portal.TestUtilities.Helpers;

/// <summary>
/// Factory for creating HouseholdData instances using Bogus for generating fake data.
/// Used for testing. For seeding and MockHouseholdRepository, use Infrastructure.Seeding.Helpers.HouseholdFactory.
/// See https://github.com/bchavez/Bogus for more information
/// </summary>
public static class HouseholdFactory
{
    private static readonly Faker<HouseholdData> HouseholdDataFaker = new Faker<HouseholdData>()
        .RuleFor(h => h.Email, f => f.Internet.Email().ToLowerInvariant())
        .RuleFor(h => h.Phone, f => f.Phone.PhoneNumber("###-####"))
        .RuleFor(h => h.BenefitIssuanceType, f => f.PickRandom<BenefitIssuanceType>())
        .RuleFor(h => h.ApplicationStatus, f => f.PickRandom<ApplicationStatus>())
        .RuleFor(h => h.ApplicationNumber, (f, h) =>
            h.ApplicationStatus != ApplicationStatus.Unknown
                ? $"APP-{f.Date.Recent(365):yyyy-MM}-{f.Random.Number(100000, 999999)}"
                : null)
        .RuleFor(h => h.CaseNumber, (f, h) =>
            h.ApplicationStatus == ApplicationStatus.Approved || h.ApplicationStatus == ApplicationStatus.Denied
                ? $"CASE-{f.Random.Number(100000, 999999)}"
                : null)
        .RuleFor(h => h.BenefitIssueDate, (f, h) =>
            h.ApplicationStatus == ApplicationStatus.Approved
                ? f.Date.Recent(120)
                : null)
        .RuleFor(h => h.BenefitExpirationDate, (f, h) =>
            h.BenefitIssueDate.HasValue
                ? h.BenefitIssueDate.Value.AddDays(f.Random.Int(30, 365))
                : null)
        .RuleFor(h => h.Last4DigitsOfCard, (f, h) =>
            h.ApplicationStatus == ApplicationStatus.Approved
                ? f.Random.Number(1000, 9999).ToString()
                : null)
        .RuleFor(h => h.Children, f => GenerateChildren(f.Random.Int(0, 4)))
        .RuleFor(h => h.AddressOnFile, (f, h) =>
            f.Random.Bool(0.6f) && h.ApplicationStatus == ApplicationStatus.Approved
                ? GenerateAddress(f)
                : null);

    /// <summary>
    /// Creates a new HouseholdData instance with generated fake data.
    /// </summary>
    public static HouseholdData CreateHouseholdData(Action<HouseholdData>? customize = null)
    {
        var household = HouseholdDataFaker.Generate();
        customize?.Invoke(household);
        return household;
    }

    /// <summary>
    /// Creates a new HouseholdData instance with a specific email address.
    /// </summary>
    public static HouseholdData CreateHouseholdDataWithEmail(string email, Action<HouseholdData>? customize = null)
    {
        var household = HouseholdDataFaker.Generate();
        household.Email = string.IsNullOrWhiteSpace(email) ? email : EmailNormalizer.Normalize(email);
        customize?.Invoke(household);
        return household;
    }

    /// <summary>
    /// Creates a HouseholdData with a specific application status.
    /// </summary>
    public static HouseholdData CreateHouseholdDataWithStatus(
        ApplicationStatus status,
        Action<HouseholdData>? customize = null)
    {
        return CreateHouseholdData(h =>
        {
            var faker = new Faker();
            h.ApplicationStatus = status;

            if (status == ApplicationStatus.Approved)
            {
                h.BenefitIssueDate = faker.Date.Recent(120);
                h.BenefitExpirationDate = h.BenefitIssueDate.Value.AddDays(faker.Random.Int(30, 365));
                h.Last4DigitsOfCard = faker.Random.Number(1000, 9999).ToString();
                h.CaseNumber = $"CASE-{faker.Random.Number(100000, 999999)}";
                h.ApplicationNumber = $"APP-{faker.Date.Recent(365):yyyy-MM}-{faker.Random.Number(100000, 999999)}";
            }
            else if (status == ApplicationStatus.Denied)
            {
                h.CaseNumber = $"CASE-{faker.Random.Number(100000, 999999)}";
                h.ApplicationNumber = $"APP-{faker.Date.Recent(365):yyyy-MM}-{faker.Random.Number(100000, 999999)}";
                h.BenefitIssueDate = null;
                h.BenefitExpirationDate = null;
                h.Last4DigitsOfCard = null;
            }
            else if (status == ApplicationStatus.Unknown)
            {
                h.BenefitIssueDate = null;
                h.BenefitExpirationDate = null;
                h.Last4DigitsOfCard = null;
                h.CaseNumber = null;
                h.ApplicationNumber = null;
            }
            else
            {
                h.ApplicationNumber = $"APP-{faker.Date.Recent(365):yyyy-MM}-{faker.Random.Number(100000, 999999)}";
                h.CaseNumber = null;
            }

            customize?.Invoke(h);
        });
    }

    /// <summary>
    /// Creates a HouseholdData with an address (simulating ID verified user).
    /// </summary>
    public static HouseholdData CreateHouseholdDataWithAddress(Action<HouseholdData>? customize = null)
    {
        return CreateHouseholdData(h =>
        {
            var faker = new Faker();
            h.AddressOnFile = GenerateAddress(faker);
            customize?.Invoke(h);
        });
    }

    /// <summary>
    /// Sets a seed for the random number generator to ensure deterministic test data.
    /// </summary>
    public static void SetSeed(int seed)
    {
        Randomizer.Seed = new Random(seed);
    }

    private static List<Child> GenerateChildren(int count)
    {
        if (count <= 0)
        {
            return new List<Child>();
        }

        var faker = new Faker<Child>()
            .RuleFor(c => c.FirstName, f => f.Name.FirstName())
            .RuleFor(c => c.LastName, f => f.Name.LastName());

        return faker.Generate(count);
    }

    private static Address GenerateAddress(Faker faker)
    {
        return new Address
        {
            StreetAddress1 = faker.Address.StreetAddress(),
            StreetAddress2 = faker.Random.Bool(0.3f) ? faker.Address.SecondaryAddress() : null,
            City = faker.Address.City(),
            State = faker.Address.StateAbbr(),
            PostalCode = faker.Address.ZipCode()
        };
    }
}
