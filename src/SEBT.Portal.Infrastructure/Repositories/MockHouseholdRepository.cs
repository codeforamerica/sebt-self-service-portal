using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Bogus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Seeding;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Utilities;
using SEBT.Portal.TestUtilities.Helpers;

namespace SEBT.Portal.Infrastructure.Repositories;

/// <summary>
/// Mock implementation of household repository for development and testing.
/// Returns mock data without requiring a database or external service.
/// </summary>
public class MockHouseholdRepository : IHouseholdRepository
{
    private readonly ConcurrentDictionary<string, HouseholdData> _households;
    private readonly ConcurrentDictionary<string, HouseholdData> _householdsByPhone;
    private readonly SeedingSettings _settings;
    private readonly ILogger<MockHouseholdRepository> _logger;
    private readonly TimeProvider _timeProvider;

    public MockHouseholdRepository(
        ILogger<MockHouseholdRepository> logger,
        IOptions<SeedingSettings>? settings = null,
        TimeProvider? timeProvider = null)
    {
        _logger = logger;
        _settings = settings?.Value ?? new SeedingSettings();
        _timeProvider = timeProvider ?? TimeProvider.System;
        _households = new ConcurrentDictionary<string, HouseholdData>();
        _householdsByPhone = new ConcurrentDictionary<string, HouseholdData>();
        SeedMockData();
    }

    public Task<HouseholdData?> GetHouseholdByIdentifierAsync(
        HouseholdIdentifier identifier,
        PiiVisibility piiVisibility,
        UserIalLevel userIalLevel,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(piiVisibility);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(identifier.Value))
        {
            return Task.FromResult<HouseholdData?>(null);
        }

        HouseholdData? household = null;
        string? lookupValue = null;

        if (identifier.Type == PreferredHouseholdIdType.Email)
        {
            lookupValue = EmailNormalizer.Normalize(identifier.Value);
            _households.TryGetValue(lookupValue, out household);
        }
        else if (identifier.Type == PreferredHouseholdIdType.Phone)
        {
            lookupValue = NormalizePhone(identifier.Value);
            if (!string.IsNullOrEmpty(lookupValue))
            {
                _householdsByPhone.TryGetValue(lookupValue, out household);
            }
        }

        if (identifier.Type != PreferredHouseholdIdType.Email && identifier.Type != PreferredHouseholdIdType.Phone)
        {
            _logger.LogDebug(
                "Mock household lookup by {Type} not supported; only Email and Phone are keyed in mock data",
                identifier.Type);
            return Task.FromResult<HouseholdData?>(null);
        }

        if (household == null)
        {
            _logger.LogInformation("Mock household not found for identifier type {Type}", identifier.Type);
            return Task.FromResult<HouseholdData?>(null);
        }

        var result = CreateCopy(household, piiVisibility);
        _logger.LogDebug(
            "Returning mock household data for identifier type {Type}, PII visibility: Address={IncludeAddress}, Email={IncludeEmail}, Phone={IncludePhone}",
            identifier.Type,
            piiVisibility.IncludeAddress,
            piiVisibility.IncludeEmail,
            piiVisibility.IncludePhone);

        return Task.FromResult<HouseholdData?>(result);
    }

    public Task<HouseholdData?> GetHouseholdByEmailAsync(
        string email,
        PiiVisibility piiVisibility,
        UserIalLevel userIalLevel,
        CancellationToken cancellationToken = default)
    {
        return GetHouseholdByIdentifierAsync(HouseholdIdentifier.Email(email), piiVisibility, userIalLevel, cancellationToken);
    }

    public Task UpsertHouseholdAsync(
        HouseholdData householdData,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (householdData == null)
        {
            throw new ArgumentNullException(nameof(householdData));
        }

        if (string.IsNullOrWhiteSpace(householdData.Email))
        {
            throw new ArgumentException("Email cannot be null or empty.", nameof(householdData));
        }

        var normalizedEmail = EmailNormalizer.Normalize(householdData.Email);

        // Create a defensive copy to prevent external mutations
        var fullVisibility = new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: true);
        var copy = CreateCopy(householdData, fullVisibility);
        _households[normalizedEmail] = copy;
        IndexByPhone(copy);

        _logger.LogInformation("Mock household data updated for email {Email}", normalizedEmail);
        return Task.CompletedTask;
    }

    private void SeedMockData()
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Use a fixed seed for deterministic data generation across runs
        HouseholdFactory.SetSeed(12345);

        // Scenario 1: Co-loaded user with approved application and address (ID verified)
        var coLoadedEmail = _settings.BuildEmail(SeedScenarios.CoLoaded.Name);
        var coLoaded = HouseholdFactory.CreateHouseholdDataWithStatus(ApplicationStatus.Approved, h =>
        {
            h.BenefitIssuanceType = BenefitIssuanceType.SnapEbtCard;
            var app = h.Applications.FirstOrDefault();
            if (app != null)
            {
                app.IssuanceType = IssuanceType.SnapEbtCard;
                app.BenefitIssueDate = now.AddDays(-20);
                app.BenefitExpirationDate = now.AddDays(70);
                app.Last4DigitsOfCard = "0000";
                app.CardStatus = CardStatus.Active;
                // Set specific children names for test
                app.Children = new List<Child>
                {
                    new Child { FirstName = "Sophia", LastName = "Martinez" },
                    new Child { FirstName = "James", LastName = "Martinez" }
                };
            }
            h.AddressOnFile = new Address
            {
                StreetAddress1 = "100 Co-Loaded Street",
                StreetAddress2 = "Suite 100",
                City = "Denver",
                State = "CO",
                PostalCode = "80201"
            };
        });
        coLoaded.Email = coLoadedEmail;
        coLoaded.Phone = "8185558437"; // Matches default DevelopmentPhoneOverride for mock + phone lookup in dev
        coLoaded.UserProfile = new UserProfile { FirstName = "Maria", MiddleName = "Elena", LastName = "Martinez" };
        _households[coLoadedEmail] = coLoaded;
        IndexByPhone(coLoaded);

        // Scenario 2: Approved application with address (ID verified user)
        var verifiedEmail = _settings.BuildEmail(SeedScenarios.Verified.Name);
        var verified = HouseholdFactory.CreateHouseholdDataWithStatus(ApplicationStatus.Approved, h =>
        {
            h.BenefitIssuanceType = BenefitIssuanceType.SnapEbtCard;
            var app = h.Applications.FirstOrDefault();
            if (app != null)
            {
                app.ApplicationNumber = "APP-2025-01-100001";
                app.CaseNumber = "CASE-100001";
                app.IssuanceType = IssuanceType.SummerEbt;
                app.BenefitIssueDate = now.AddDays(-30);
                app.BenefitExpirationDate = now.AddDays(60);
                app.Last4DigitsOfCard = "1234"; // Specific value for test
                app.CardStatus = CardStatus.Active;
                // Set specific children names for test
                app.Children = new List<Child>
                {
                    new Child { FirstName = "John", LastName = "Doe" },
                    new Child { FirstName = "Jane", LastName = "Doe" }
                };
            }
            // Set specific address for test
            h.AddressOnFile = new Address
            {
                StreetAddress1 = "123 Main Street",
                StreetAddress2 = "Apt 4B",
                City = "Denver",
                State = "CO",
                PostalCode = "80202"
            };
        });
        verified.Email = verifiedEmail;
        verified.UserProfile = new UserProfile { FirstName = "John", MiddleName = "Robert", LastName = "Doe" };
        _households[verifiedEmail] = verified;
        IndexByPhone(verified);
        // To test CO OIDC login locally, uncomment and replace with your PingOne sandbox user email:
        // _households["sebt.co+YOUR_PHONE@codeforamerica.org"] = verified;

        // Scenario 3: Pending application without address (not ID verified)
        // Note: Address should not be included for non-ID-verified users, but we set it here
        // for testing purposes (it will be filtered by GetHouseholdByEmailAsync based on includeAddress)
        var pendingEmail = _settings.BuildEmail(SeedScenarios.Pending.Name);
        var pending = HouseholdFactory.CreateHouseholdDataWithStatus(ApplicationStatus.Pending, h =>
        {
            h.BenefitIssuanceType = BenefitIssuanceType.Unknown;
            var app = h.Applications.FirstOrDefault();
            if (app != null)
            {
                // Set specific child name for test
                app.Children = new List<Child>
                {
                    new Child { FirstName = "Alice", LastName = "Smith" }
                };
            }
            // Set address for testing (will be filtered based on ID verification status)
            h.AddressOnFile = new Address
            {
                StreetAddress1 = "456 Oak Avenue",
                City = "Boulder",
                State = "CO",
                PostalCode = "80301"
            };
        });
        pending.Email = pendingEmail;
        pending.UserProfile = new UserProfile { FirstName = "Jane", MiddleName = "Marie", LastName = "Smith" };
        _households[pendingEmail] = pending;
        IndexByPhone(pending);

        // Scenario 4: Denied application
        var deniedEmail = _settings.BuildEmail(SeedScenarios.Denied.Name);
        var denied = HouseholdFactory.CreateHouseholdDataWithStatus(ApplicationStatus.Denied, h =>
        {
            h.BenefitIssuanceType = BenefitIssuanceType.Unknown;
            var app = h.Applications.FirstOrDefault();
            if (app != null)
            {
                app.Children = new List<Child>(); // No children for denied
            }
        });
        denied.Email = deniedEmail;
        denied.UserProfile = new UserProfile { FirstName = "Robert", MiddleName = null, LastName = "Johnson" };
        _households[deniedEmail] = denied;
        IndexByPhone(denied);

        // Scenario 5: Under review
        var reviewEmail = _settings.BuildEmail(SeedScenarios.Review.Name);
        var review = HouseholdFactory.CreateHouseholdDataWithStatus(ApplicationStatus.Unknown, h =>
        {
            h.BenefitIssuanceType = BenefitIssuanceType.SummerEbt;
            var app = h.Applications.FirstOrDefault();
            if (app != null)
            {
                app.CardStatus = CardStatus.Active;
                // Use Bogus to generate child name
                var childFaker = new Faker<Child>()
                    .RuleFor(c => c.FirstName, f => f.Name.FirstName())
                    .RuleFor(c => c.LastName, f => f.Name.LastName());
                app.Children = childFaker.Generate(1);
                app.CardRequestedAt = now.AddDays(-7);
            }
        });
        review.Email = reviewEmail;
        review.UserProfile = new UserProfile { FirstName = "Susan", MiddleName = "Lee", LastName = "Williams" };
        _households[reviewEmail] = review;
        IndexByPhone(review);

        // Scenario 5b: Non-co-loaded user (ID proofing in progress)
        var nonCoLoadedEmail = _settings.BuildEmail(SeedScenarios.NonCoLoaded.Name);
        var nonCoLoaded = HouseholdFactory.CreateHouseholdDataWithStatus(ApplicationStatus.Pending, h =>
        {
            h.BenefitIssuanceType = BenefitIssuanceType.SummerEbt;
            var app = h.Applications.FirstOrDefault();
            if (app != null)
            {
                app.Children = new List<Child>
                {
                    new Child { FirstName = "Emma", LastName = "Garcia" }
                };
            }
            h.AddressOnFile = null;
        });
        nonCoLoaded.Email = nonCoLoadedEmail;
        nonCoLoaded.Phone = "5551234567";
        nonCoLoaded.UserProfile = new UserProfile { FirstName = "Carlos", MiddleName = "Miguel", LastName = "Garcia" };
        _households[nonCoLoadedEmail] = nonCoLoaded;
        IndexByPhone(nonCoLoaded);

        // Scenario 5c: Not-started user (ID proofing not started)
        var notStartedEmail = _settings.BuildEmail(SeedScenarios.NotStarted.Name);
        var notStarted = HouseholdFactory.CreateHouseholdDataWithStatus(ApplicationStatus.Pending, h =>
        {
            h.BenefitIssuanceType = BenefitIssuanceType.TanfEbtCard;
            var app = h.Applications.FirstOrDefault();
            if (app != null)
            {
                app.Children = new List<Child>
                {
                    new Child { FirstName = "Liam", LastName = "Anderson" }
                };
            }
            h.AddressOnFile = new Address
            {
                StreetAddress1 = "321 Not Started Drive",
                City = "Denver",
                State = "CO",
                PostalCode = "80205"
            };
        });
        notStarted.Email = notStartedEmail;
        notStarted.Phone = "5559876543";
        notStarted.UserProfile = new UserProfile { FirstName = "Jordan", MiddleName = "Lee", LastName = "Anderson" };
        _households[notStartedEmail] = notStarted;
        IndexByPhone(notStarted);

        // Scenario 6: Cancelled application
        var cancelledEmail = _settings.BuildEmail(SeedScenarios.Cancelled.Name);
        var cancelled = HouseholdFactory.CreateHouseholdDataWithStatus(ApplicationStatus.Cancelled, h =>
        {
            h.BenefitIssuanceType = BenefitIssuanceType.Unknown;
            var app = h.Applications.FirstOrDefault();
            if (app != null)
            {
                app.Children = new List<Child>(); // No children for cancelled
            }
        });
        cancelled.Email = cancelledEmail;
        cancelled.UserProfile = new UserProfile { FirstName = "David", MiddleName = "James", LastName = "Davis" };
        _households[cancelledEmail] = cancelled;
        IndexByPhone(cancelled);

        // Scenario 7: Approved with single child
        var singleChildEmail = _settings.BuildEmail(SeedScenarios.SingleChild.Name);
        var singleChild = HouseholdFactory.CreateHouseholdDataWithStatus(ApplicationStatus.Pending, h =>
        {
            h.BenefitIssuanceType = BenefitIssuanceType.SummerEbt;
            var app = h.Applications.FirstOrDefault();
            if (app != null)
            {
                app.IssuanceType = IssuanceType.SummerEbt;
                app.BenefitIssueDate = now.AddDays(-15);
                app.BenefitExpirationDate = now.AddDays(75);
                app.CardStatus = CardStatus.Active;
                // Use Bogus to generate child name
                var childFaker = new Faker<Child>()
                    .RuleFor(c => c.FirstName, f => f.Name.FirstName())
                    .RuleFor(c => c.LastName, f => f.Name.LastName());
                app.Children = childFaker.Generate(1);
            }
        });
        singleChild.Email = singleChildEmail;
        singleChild.UserProfile = new UserProfile { FirstName = "Amanda", MiddleName = "Rose", LastName = "Taylor" };
        _households[singleChildEmail] = singleChild;
        IndexByPhone(singleChild);

        // Scenario 8: Large family (multiple children)
        var largeFamilyEmail = _settings.BuildEmail(SeedScenarios.LargeFamily.Name);
        var largeFamily = HouseholdFactory.CreateHouseholdDataWithStatus(ApplicationStatus.Unknown, h =>
        {
            h.BenefitIssuanceType = BenefitIssuanceType.SummerEbt;
            var app = h.Applications.FirstOrDefault();
            if (app != null)
            {
                app.IssuanceType = IssuanceType.TanfEbtCard;
                app.BenefitIssueDate = now.AddDays(-45);
                app.BenefitExpirationDate = now.AddDays(45);
                app.Last4DigitsOfCard = "4321";
                app.CardStatus = CardStatus.Active;
                // Set specific children names for test
                app.Children = new List<Child>
                {
                    new Child { FirstName = "Michael", LastName = "Brown" },
                    new Child { FirstName = "Sarah", LastName = "Brown" },
                    new Child { FirstName = "David", LastName = "Brown" },
                    new Child { FirstName = "Emily", LastName = "Brown" }
                };
            }
            h.AddressOnFile = new Address
            {
                StreetAddress1 = "456 Large Family Lane",
                StreetAddress2 = "Unit 8",
                City = "Aurora",
                State = "CO",
                PostalCode = "80010"
            };
        });
        largeFamily.Email = largeFamilyEmail;
        largeFamily.UserProfile = new UserProfile { FirstName = "Christopher", MiddleName = "Michael", LastName = "Brown" };
        _households[largeFamilyEmail] = largeFamily;
        IndexByPhone(largeFamily);

        // Scenario 9: Minimal data (no phone, no dates)
        var minimalEmail = _settings.BuildEmail(SeedScenarios.Minimal.Name);
        var minimal = HouseholdFactory.CreateHouseholdDataWithStatus(ApplicationStatus.Pending, h =>
        {
            h.BenefitIssuanceType = BenefitIssuanceType.Unknown;
            h.Phone = null;
            var app = h.Applications.FirstOrDefault();
            if (app != null)
            {
                app.Children = new List<Child>();
            }
        });
        minimal.Email = minimalEmail;
        minimal.UserProfile = new UserProfile { FirstName = "Alex", MiddleName = null, LastName = "Jones" };
        _households[minimalEmail] = minimal;
        IndexByPhone(minimal);

        // Scenario 10: Expired benefits
        var expiredEmail = _settings.BuildEmail(SeedScenarios.Expired.Name);
        var expired = HouseholdFactory.CreateHouseholdDataWithStatus(ApplicationStatus.Unknown, h =>
        {
            h.BenefitIssuanceType = BenefitIssuanceType.SummerEbt;
            var app = h.Applications.FirstOrDefault();
            if (app != null)
            {
                app.BenefitIssueDate = now.AddDays(-120);
                app.BenefitExpirationDate = now.AddDays(-10); // Expired
                app.CardStatus = CardStatus.Deactivated;
                // Use Bogus to generate child name
                var childFaker = new Faker<Child>()
                    .RuleFor(c => c.FirstName, f => f.Name.FirstName())
                    .RuleFor(c => c.LastName, f => f.Name.LastName());
                app.Children = childFaker.Generate(1);
            }
        });
        expired.Email = expiredEmail;
        expired.UserProfile = new UserProfile { FirstName = "Patricia", MiddleName = "Ann", LastName = "Garcia" };
        _households[expiredEmail] = expired;
        IndexByPhone(expired);

        // Scenario 11: Unknown status
        var unknownEmail = _settings.BuildEmail(SeedScenarios.Unknown.Name);
        var unknown = HouseholdFactory.CreateHouseholdDataWithStatus(ApplicationStatus.Unknown, h =>
        {
            h.BenefitIssuanceType = BenefitIssuanceType.Unknown;
            var app = h.Applications.FirstOrDefault();
            if (app != null)
            {
                app.Children = new List<Child>();
            }
        });
        unknown.Email = unknownEmail;
        unknown.UserProfile = new UserProfile { FirstName = "Unknown", MiddleName = null, LastName = "User" };
        _households[unknownEmail] = unknown;
        IndexByPhone(unknown);

        // Scenario 12: Household with multiple applications (approved and pending)
        var multipleAppsEmail = _settings.BuildEmail(SeedScenarios.MultipleApps.Name);
        var multipleApps = HouseholdFactory.CreateHouseholdDataWithStatus(ApplicationStatus.Approved, h =>
        {
            h.BenefitIssuanceType = BenefitIssuanceType.SnapEbtCard;
            var faker = new Faker { Random = new Randomizer(42) };
            var approvedApp = new Application
            {
                ApplicationNumber = $"APP-{now.AddDays(-30):yyyy-MM}-{faker.Random.Number(100000, 999999)}",
                CaseNumber = $"CASE-{faker.Random.Number(100000, 999999)}",
                ApplicationStatus = ApplicationStatus.Approved,
                IssuanceType = IssuanceType.SummerEbt,
                BenefitIssueDate = now.AddDays(-30),
                BenefitExpirationDate = now.AddDays(60),
                Last4DigitsOfCard = "5678",
                CardStatus = CardStatus.Active,
                CardRequestedAt = now.AddDays(-60),
                CardMailedAt = now.AddDays(-45),
                CardActivatedAt = now.AddDays(-40),
                Children = new List<Child>
                {
                    new Child { FirstName = "Emma", LastName = "Wilson" },
                    new Child { FirstName = "Lucas", LastName = "Wilson" }
                }
            };

            var pendingApp = new Application
            {
                ApplicationNumber = $"APP-{now.AddDays(-10):yyyy-MM}-{faker.Random.Number(100000, 999999)}",
                ApplicationStatus = ApplicationStatus.Pending,
                CardStatus = CardStatus.Requested,
                CardRequestedAt = now.AddDays(-10),
                Children = new List<Child>
                {
                    new Child { FirstName = "Olivia", LastName = "Wilson" }
                }
            };

            h.Applications = new List<Application> { approvedApp, pendingApp };
            h.AddressOnFile = new Address
            {
                StreetAddress1 = "789 Multiple Apps Street",
                City = "Denver",
                State = "CO",
                PostalCode = "80203"
            };
        });
        multipleApps.Email = multipleAppsEmail;
        multipleApps.UserProfile = new UserProfile { FirstName = "Jennifer", MiddleName = "Lynn", LastName = "Wilson" };
        _households[multipleAppsEmail] = multipleApps;
        IndexByPhone(multipleApps);

        // DC Scenarios 1-7: Simple non-co-loaded households with 1 child, Summer EBT, active benefits
        if (string.Equals(_settings.State, "dc", StringComparison.OrdinalIgnoreCase))
        {
            var dcChildFaker = new Faker<Child>()
                .RuleFor(c => c.FirstName, f => f.Name.FirstName())
                .RuleFor(c => c.LastName, f => f.Name.LastName());

            var dcUserFaker = new Faker();

            SeedScenario[] dcScenarioList = [SeedScenarios.Simple1, SeedScenarios.Simple2, SeedScenarios.Simple3,
                SeedScenarios.Simple4, SeedScenarios.Simple5, SeedScenarios.Simple6, SeedScenarios.Simple7];

            foreach (var dcScenario in dcScenarioList)
            {
                var dcEmail = _settings.BuildEmail(dcScenario.Name);
                var dcHousehold = HouseholdFactory.CreateHouseholdDataWithStatus(ApplicationStatus.Approved, h =>
                {
                    h.BenefitIssuanceType = BenefitIssuanceType.SummerEbt;
                    var app = h.Applications.FirstOrDefault();
                    if (app != null)
                    {
                        app.BenefitIssueDate = now.AddDays(-20);
                        app.BenefitExpirationDate = now.AddDays(122);
                        app.CardStatus = CardStatus.Active;
                        app.Children = dcChildFaker.Generate(1);
                    }
                    h.AddressOnFile = new Address
                    {
                        StreetAddress1 = dcUserFaker.Address.StreetAddress(),
                        City = "Washington",
                        State = "DC",
                        PostalCode = "20002"
                    };
                });
                dcHousehold.Email = dcEmail;
                dcHousehold.UserProfile = new UserProfile
                {
                    FirstName = dcUserFaker.Name.FirstName(),
                    MiddleName = null,
                    LastName = dcUserFaker.Name.LastName()
                };
                _households[dcEmail] = dcHousehold;
                IndexByPhone(dcHousehold);
            }
        }

        _logger.LogInformation("Seeded {Count} mock household records using Bogus", _households.Count);
    }

    private static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        var digits = Regex.Replace(phone.Trim(), @"\D", "");
        return string.IsNullOrEmpty(digits) ? null : digits;
    }

    private void IndexByPhone(HouseholdData household)
    {
        var normalized = NormalizePhone(household.Phone);
        if (!string.IsNullOrEmpty(normalized))
        {
            _householdsByPhone[normalized] = household;
        }
    }

    /// <summary>
    /// Creates a defensive copy of household data to prevent external mutations.
    /// PII fields are filtered based on the visibility flags.
    /// </summary>
    /// <param name="source">The source household data to copy.</param>
    /// <param name="piiVisibility">Which PII elements to include in the copy.</param>
    /// <returns>A new instance of HouseholdData with copied values.</returns>
    private static HouseholdData CreateCopy(HouseholdData source, PiiVisibility piiVisibility)
    {
        return source with
        {
            Email = piiVisibility.IncludeEmail ? source.Email : PiiMasker.MaskEmail(source.Email),
            Phone = piiVisibility.IncludePhone ? source.Phone : PiiMasker.MaskPhone(source.Phone),
            AddressOnFile = piiVisibility.IncludeAddress && source.AddressOnFile != null
                ? new Address
                {
                    StreetAddress1 = source.AddressOnFile.StreetAddress1,
                    StreetAddress2 = source.AddressOnFile.StreetAddress2,
                    City = source.AddressOnFile.City,
                    State = source.AddressOnFile.State,
                    PostalCode = source.AddressOnFile.PostalCode
                }
                : source.AddressOnFile != null
                    ? new Address
                    {
                        StreetAddress1 = PiiMasker.MaskStreetAddress(source.AddressOnFile.StreetAddress1, source.AddressOnFile.StreetAddress2),
                        City = source.AddressOnFile.City,
                        State = source.AddressOnFile.State,
                        PostalCode = source.AddressOnFile.PostalCode
                    }
                    : null,
            BenefitIssuanceType = source.BenefitIssuanceType,
            SummerEbtCases = source.SummerEbtCases.Select(sec => new SummerEbtCase
            {
                SummerEBTCaseID = sec.SummerEBTCaseID,
                ApplicationId = sec.ApplicationId,
                ApplicationStudentId = sec.ApplicationStudentId,
                ChildFirstName = sec.ChildFirstName,
                ChildLastName = sec.ChildLastName,
                ChildDateOfBirth = sec.ChildDateOfBirth,
                HouseholdType = sec.HouseholdType,
                EligibilityType = sec.EligibilityType,
                ApplicationDate = sec.ApplicationDate,
                ApplicationStatus = sec.ApplicationStatus,
                MailingAddress = sec.MailingAddress != null && piiVisibility.IncludeAddress
                    ? new Address
                    {
                        StreetAddress1 = sec.MailingAddress.StreetAddress1,
                        StreetAddress2 = sec.MailingAddress.StreetAddress2,
                        City = sec.MailingAddress.City,
                        State = sec.MailingAddress.State,
                        PostalCode = sec.MailingAddress.PostalCode
                    }
                    : null,
                EbtCaseNumber = sec.EbtCaseNumber,
                EbtCardLastFour = sec.EbtCardLastFour,
                EbtCardStatus = sec.EbtCardStatus,
                EbtCardIssueDate = sec.EbtCardIssueDate,
                EbtCardBalance = sec.EbtCardBalance,
                BenefitAvailableDate = sec.BenefitAvailableDate,
                BenefitExpirationDate = sec.BenefitExpirationDate
            }).ToList(),
            UserProfile = source.UserProfile != null
                ? new UserProfile
                {
                    FirstName = source.UserProfile.FirstName,
                    MiddleName = source.UserProfile.MiddleName,
                    LastName = source.UserProfile.LastName
                }
                : null,
            Applications = source.Applications.Select(a => new Application
            {
                ApplicationNumber = a.ApplicationNumber,
                CaseNumber = a.CaseNumber,
                ApplicationStatus = a.ApplicationStatus,
                BenefitIssueDate = a.BenefitIssueDate,
                BenefitExpirationDate = a.BenefitExpirationDate,
                Last4DigitsOfCard = a.Last4DigitsOfCard,
                CardStatus = a.CardStatus,
                CardRequestedAt = a.CardRequestedAt,
                CardMailedAt = a.CardMailedAt,
                CardActivatedAt = a.CardActivatedAt,
                CardDeactivatedAt = a.CardDeactivatedAt,
                // If application-level issuance type isn't set, inherit from the
                // household-level BenefitIssuanceType (both enums share the same values)
                IssuanceType = a.IssuanceType != IssuanceType.Unknown
                    ? a.IssuanceType
                    : (IssuanceType)(int)source.BenefitIssuanceType,
                Children = a.Children.Select(c => new Child
                {
                    FirstName = c.FirstName,
                    LastName = c.LastName,
                    Status = c.Status
                }).ToList()
            }).ToList()
        };
    }
}
