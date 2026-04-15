using Bogus;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Utilities;

namespace SEBT.Portal.TestUtilities.Helpers;

/// <summary>
/// Factory for creating HouseholdData instances using Bogus for generating fake data.
/// Used for unit tests and by MockHouseholdRepository for development mock data.
/// See https://github.com/bchavez/Bogus for more information
/// </summary>
public static class HouseholdFactory
{
    private static readonly Faker<HouseholdData> HouseholdDataFaker = new Faker<HouseholdData>()
        .RuleFor(h => h.Email, f => f.Internet.Email().ToLowerInvariant())
        .RuleFor(h => h.Phone, f => f.Phone.PhoneNumber("###-####"))
        .RuleFor(h => h.BenefitIssuanceType, f => f.PickRandom<BenefitIssuanceType>())
        .RuleFor(h => h.SummerEbtCases, _ => new List<SummerEbtCase>())
        .RuleFor(h => h.Applications, f => GenerateApplications(f))
        .RuleFor(h => h.AddressOnFile, (f, h) =>
            f.Random.Bool(0.6f) && h.Applications.Any(a => a.ApplicationStatus == ApplicationStatus.Approved)
                ? GenerateAddress(f)
                : null);

    /// <summary>
    /// Creates a new HouseholdData instance with generated fake data.
    /// </summary>
    /// <param name="customize">Optional action to customize the generated household.</param>
    /// <returns>A new HouseholdData instance.</returns>
    public static HouseholdData CreateHouseholdData(Action<HouseholdData>? customize = null)
    {
        var household = HouseholdDataFaker.Generate();
        customize?.Invoke(household);
        return household;
    }

    /// <summary>
    /// Creates a new HouseholdData instance with a specific email address.
    /// Note: For testing purposes, this allows empty/null emails to test repository validation.
    /// In production code, emails should be validated before calling this method.
    /// </summary>
    /// <param name="email">The email address to use (may be empty/null for testing).</param>
    /// <param name="customize">Optional action to further customize the household.</param>
    /// <returns>A new HouseholdData instance with the specified email.</returns>
    public static HouseholdData CreateHouseholdDataWithEmail(string email, Action<HouseholdData>? customize = null)
    {
        var household = HouseholdDataFaker.Generate();
        // Only normalize if email is not empty/null
        household.Email = string.IsNullOrWhiteSpace(email) ? email : EmailNormalizer.Normalize(email);
        customize?.Invoke(household);
        return household;
    }

    /// <summary>
    /// Creates a HouseholdData with a specific application status.
    /// </summary>
    /// <param name="status">The application status to set.</param>
    /// <param name="customize">Optional action to further customize the household.</param>
    /// <returns>A HouseholdData instance with the specified application status.</returns>
    public static HouseholdData CreateHouseholdDataWithStatus(
        ApplicationStatus status,
        Action<HouseholdData>? customize = null)
    {
        return CreateHouseholdData(h =>
        {
            var faker = new Faker();
            var application = CreateApplicationWithStatus(status, faker);
            h.Applications = new List<Application> { application };

            customize?.Invoke(h);
        });
    }

    /// <summary>
    /// Creates a HouseholdData with an address (simulating ID verified user).
    /// </summary>
    /// <param name="customize">Optional action to further customize the household.</param>
    /// <returns>A HouseholdData instance with an address.</returns>
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
    /// Creates a SummerEbtCase for an enrolled child with sensible defaults.
    /// </summary>
    /// <param name="childFirstName">The child's first name.</param>
    /// <param name="childLastName">The child's last name.</param>
    /// <param name="eligibilityType">How the child became eligible (e.g., SNAP, TANF, NSLP, Medicaid, CategoricalEligibility, Application).</param>
    /// <param name="customize">Optional action to override defaults.</param>
    /// <returns>A new SummerEbtCase instance.</returns>
    public static SummerEbtCase CreateSummerEbtCase(
        string childFirstName,
        string childLastName,
        string eligibilityType,
        Action<SummerEbtCase>? customize = null)
    {
        var faker = new Faker();
        var benefitStart = new DateTime(2026, 5, 4);

        var summerEbtCase = new SummerEbtCase
        {
            SummerEBTCaseID = faker.Random.Number(100000, 999999).ToString(),
            ChildFirstName = childFirstName,
            ChildLastName = childLastName,
            ChildDateOfBirth = faker.Date.Between(
                DateTime.Today.AddYears(-17),
                DateTime.Today.AddYears(-5)),
            EligibilityType = eligibilityType,
            ApplicationStatus = ApplicationStatus.Approved,
            IssuanceType = IssuanceType.SummerEbt,
            EbtCardStatus = "Active",
            BenefitAvailableDate = benefitStart,
            BenefitExpirationDate = benefitStart.AddDays(122)
        };

        customize?.Invoke(summerEbtCase);
        return summerEbtCase;
    }

    /// <summary>
    /// Sets a seed for the random number generator to ensure deterministic test data.
    /// </summary>
    /// <param name="seed">The seed value to use.</param>
    public static void SetSeed(int seed)
    {
        Randomizer.Seed = new Random(seed);
    }

    private static List<Application> GenerateApplications(Faker faker)
    {
        // Generate 1-2 applications per household
        var applicationCount = faker.Random.Int(1, 2);
        var applications = new List<Application>();

        for (int i = 0; i < applicationCount; i++)
        {
            var status = faker.PickRandom<ApplicationStatus>();
            applications.Add(CreateApplicationWithStatus(status, faker));
        }

        return applications;
    }

    private static Application CreateApplicationWithStatus(ApplicationStatus status, Faker faker)
    {
        var application = new Application
        {
            ApplicationStatus = status,
            Children = GenerateChildren(faker.Random.Int(0, 4))
        };

        if (status == ApplicationStatus.Approved)
        {
            application.ApplicationNumber = $"APP-{faker.Random.Number(2024, 2026)}-{faker.Random.Number(1, 12):D2}-{faker.Random.Number(100000, 999999)}";
            application.CaseNumber = $"CASE-{faker.Random.Number(100000, 999999)}";
            application.BenefitIssueDate = faker.Date.Recent(120);
            application.BenefitExpirationDate = application.BenefitIssueDate.Value.AddDays(faker.Random.Int(30, 365));
            application.Last4DigitsOfCard = faker.Random.Number(1000, 9999).ToString();
            application.CardStatus = CardStatus.Active;
            var requestedDate = faker.Date.Recent(150);
            var mailedDate = requestedDate.AddDays(faker.Random.Int(5, 30));
            var activatedDate = mailedDate.AddDays(faker.Random.Int(1, 14));
            application.CardRequestedAt = requestedDate;
            application.CardMailedAt = mailedDate;
            application.CardActivatedAt = activatedDate;
        }
        else if (status == ApplicationStatus.Denied)
        {
            application.ApplicationNumber = $"APP-{faker.Random.Number(2024, 2026)}-{faker.Random.Number(1, 12):D2}-{faker.Random.Number(100000, 999999)}";
            application.CaseNumber = $"CASE-{faker.Random.Number(100000, 999999)}";
            if (faker.Random.Bool(0.5f))
            {
                application.CardStatus = CardStatus.Requested;
                application.CardRequestedAt = faker.Date.Recent(90);
            }
            else
            {
                application.CardStatus = CardStatus.Deactivated;
                var requestedDate = faker.Date.Recent(120);
                var mailedDate = requestedDate.AddDays(faker.Random.Int(5, 30));
                var activatedDate = mailedDate.AddDays(faker.Random.Int(1, 14));
                var deactivatedDate = activatedDate.AddDays(faker.Random.Int(1, 60));
                application.CardRequestedAt = requestedDate;
                application.CardMailedAt = mailedDate;
                application.CardActivatedAt = activatedDate;
                application.CardDeactivatedAt = deactivatedDate;
            }
        }
        else if (status == ApplicationStatus.Unknown)
        {
            application.ApplicationNumber = null; // Explicitly null for Unknown status
            application.CardStatus = CardStatus.Requested;
            application.CardRequestedAt = faker.Date.Recent(60);
        }
        else
        {
            // For other statuses (Pending, UnderReview, Cancelled)
            application.ApplicationNumber = $"APP-{faker.Random.Number(2024, 2026)}-{faker.Random.Number(1, 12):D2}-{faker.Random.Number(100000, 999999)}";
            if (faker.Random.Bool(0.5f))
            {
                application.CardStatus = CardStatus.Requested;
                application.CardRequestedAt = faker.Date.Recent(90);
            }
            else
            {
                application.CardStatus = CardStatus.Mailed;
                var requestedDate = faker.Date.Recent(90);
                var mailedDate = requestedDate.AddDays(faker.Random.Int(5, 30));
                application.CardRequestedAt = requestedDate;
                application.CardMailedAt = mailedDate;
            }
        }

        return application;
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
