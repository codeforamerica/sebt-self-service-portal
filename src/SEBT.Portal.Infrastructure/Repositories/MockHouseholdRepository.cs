using System.Collections.Concurrent;
using Bogus;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<MockHouseholdRepository> _logger;
    private readonly TimeProvider _timeProvider;

    public MockHouseholdRepository(ILogger<MockHouseholdRepository> logger, TimeProvider? timeProvider = null)
    {
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _households = new ConcurrentDictionary<string, HouseholdData>();
        SeedMockData();
    }

    public Task<HouseholdData?> GetHouseholdByEmailAsync(
        string email,
        bool includeAddress = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(email))
        {
            return Task.FromResult<HouseholdData?>(null);
        }

        var normalizedEmail = EmailNormalizer.Normalize(email);
        _households.TryGetValue(normalizedEmail, out var household);

        if (household == null)
        {
            _logger.LogInformation("Mock household not found for email {Email}", normalizedEmail);
            return Task.FromResult<HouseholdData?>(null);
        }

        // Create a copy to avoid modifying the original
        var result = CreateCopy(household, includeAddress);

        _logger.LogInformation(
            "Returning mock household data for email {Email}, includeAddress: {IncludeAddress}",
            normalizedEmail,
            includeAddress);

        return Task.FromResult<HouseholdData?>(result);
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
        var copy = CreateCopy(householdData, includeAddress: true);
        _households[normalizedEmail] = copy;

        _logger.LogInformation("Mock household data updated for email {Email}", normalizedEmail);
        return Task.CompletedTask;
    }

    private void SeedMockData()
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Use a fixed seed for deterministic data generation across runs
        HouseholdFactory.SetSeed(12345);

        // Scenario 1: Co-loaded user with approved application and address (ID verified)
        var coLoaded = HouseholdFactory.CreateHouseholdDataWithStatus(ApplicationStatus.Approved, h =>
        {
            h.BenefitIssuanceType = BenefitIssuanceType.SnapEbtCard;
            var app = h.Applications.FirstOrDefault();
            if (app != null)
            {
                app.BenefitIssueDate = now.AddDays(-20);
                app.BenefitExpirationDate = now.AddDays(70);
                app.Last4DigitsOfCard = "0000";
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
        coLoaded.Email = "co-loaded@example.com";
        _households["co-loaded@example.com"] = coLoaded;

        // Scenario 2: Approved application with address (ID verified user)
        var verified = HouseholdFactory.CreateHouseholdDataWithStatus(ApplicationStatus.Approved, h =>
        {
            h.BenefitIssuanceType = BenefitIssuanceType.SnapEbtCard;
            var app = h.Applications.FirstOrDefault();
            if (app != null)
            {
                app.BenefitIssueDate = now.AddDays(-30);
                app.BenefitExpirationDate = now.AddDays(60);
                app.Last4DigitsOfCard = "1234"; // Specific value for test
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
        verified.Email = "verified@example.com";
        _households["verified@example.com"] = verified;

        // Scenario 3: Pending application without address (not ID verified)
        // Note: Address should not be included for non-ID-verified users, but we set it here
        // for testing purposes (it will be filtered by GetHouseholdByEmailAsync based on includeAddress)
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
        pending.Email = "pending@example.com";
        _households["pending@example.com"] = pending;

        // Scenario 4: Denied application
        var denied = HouseholdFactory.CreateHouseholdDataWithStatus(ApplicationStatus.Denied, h =>
        {
            h.BenefitIssuanceType = BenefitIssuanceType.Unknown;
            var app = h.Applications.FirstOrDefault();
            if (app != null)
            {
                app.Children = new List<Child>(); // No children for denied
            }
        });
        denied.Email = "denied@example.com";
        _households["denied@example.com"] = denied;

        // Scenario 5: Under review
        var review = HouseholdFactory.CreateHouseholdDataWithStatus(ApplicationStatus.UnderReview, h =>
        {
            h.BenefitIssuanceType = BenefitIssuanceType.SnapEbtCard;
            var app = h.Applications.FirstOrDefault();
            if (app != null)
            {
                // Use Bogus to generate child name
                var childFaker = new Faker<Child>()
                    .RuleFor(c => c.FirstName, f => f.Name.FirstName())
                    .RuleFor(c => c.LastName, f => f.Name.LastName());
                app.Children = childFaker.Generate(1);
            }
        });
        review.Email = "review@example.com";
        _households["review@example.com"] = review;

        // Scenario 6: Cancelled application
        var cancelled = HouseholdFactory.CreateHouseholdDataWithStatus(ApplicationStatus.Cancelled, h =>
        {
            h.BenefitIssuanceType = BenefitIssuanceType.Unknown;
            var app = h.Applications.FirstOrDefault();
            if (app != null)
            {
                app.Children = new List<Child>(); // No children for cancelled
            }
        });
        cancelled.Email = "cancelled@example.com";
        _households["cancelled@example.com"] = cancelled;

        // Scenario 7: Approved with single child
        var singleChild = HouseholdFactory.CreateHouseholdDataWithStatus(ApplicationStatus.Approved, h =>
        {
            h.BenefitIssuanceType = BenefitIssuanceType.SummerEbt;
            var app = h.Applications.FirstOrDefault();
            if (app != null)
            {
                app.BenefitIssueDate = now.AddDays(-15);
                app.BenefitExpirationDate = now.AddDays(75);
                // Use Bogus to generate child name
                var childFaker = new Faker<Child>()
                    .RuleFor(c => c.FirstName, f => f.Name.FirstName())
                    .RuleFor(c => c.LastName, f => f.Name.LastName());
                app.Children = childFaker.Generate(1);
            }
        });
        singleChild.Email = "singlechild@example.com";
        _households["singlechild@example.com"] = singleChild;

        // Scenario 8: Large family (multiple children)
        var largeFamily = HouseholdFactory.CreateHouseholdDataWithStatus(ApplicationStatus.Approved, h =>
        {
            h.BenefitIssuanceType = BenefitIssuanceType.TanfEbtCard;
            var app = h.Applications.FirstOrDefault();
            if (app != null)
            {
                app.BenefitIssueDate = now.AddDays(-45);
                app.BenefitExpirationDate = now.AddDays(45);
                // Set specific children names for test
                app.Children = new List<Child>
                {
                    new Child { FirstName = "Michael", LastName = "Brown" },
                    new Child { FirstName = "Sarah", LastName = "Brown" },
                    new Child { FirstName = "David", LastName = "Brown" },
                    new Child { FirstName = "Emily", LastName = "Brown" }
                };
            }
        });
        largeFamily.Email = "largefamily@example.com";
        _households["largefamily@example.com"] = largeFamily;

        // Scenario 9: Minimal data (no phone, no dates)
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
        minimal.Email = "minimal@example.com";
        _households["minimal@example.com"] = minimal;

        // Scenario 10: Expired benefits
        var expired = HouseholdFactory.CreateHouseholdDataWithStatus(ApplicationStatus.Approved, h =>
        {
            h.BenefitIssuanceType = BenefitIssuanceType.SnapEbtCard;
            var app = h.Applications.FirstOrDefault();
            if (app != null)
            {
                app.BenefitIssueDate = now.AddDays(-120);
                app.BenefitExpirationDate = now.AddDays(-10); // Expired
                // Use Bogus to generate child name
                var childFaker = new Faker<Child>()
                    .RuleFor(c => c.FirstName, f => f.Name.FirstName())
                    .RuleFor(c => c.LastName, f => f.Name.LastName());
                app.Children = childFaker.Generate(1);
            }
        });
        expired.Email = "expired@example.com";
        _households["expired@example.com"] = expired;

        // Scenario 11: Unknown status
        var unknown = HouseholdFactory.CreateHouseholdDataWithStatus(ApplicationStatus.Unknown, h =>
        {
            h.BenefitIssuanceType = BenefitIssuanceType.Unknown;
            var app = h.Applications.FirstOrDefault();
            if (app != null)
            {
                app.Children = new List<Child>();
            }
        });
        unknown.Email = "unknown@example.com";
        _households["unknown@example.com"] = unknown;

        // Scenario 12: Household with multiple applications (approved and pending)
        var multipleApps = HouseholdFactory.CreateHouseholdDataWithStatus(ApplicationStatus.Approved, h =>
        {
            h.BenefitIssuanceType = BenefitIssuanceType.SnapEbtCard;
            var faker = new Faker();
            var approvedApp = new Application
            {
                ApplicationNumber = $"APP-{now.AddDays(-30):yyyy-MM}-{faker.Random.Number(100000, 999999)}",
                CaseNumber = $"CASE-{faker.Random.Number(100000, 999999)}",
                ApplicationStatus = ApplicationStatus.Approved,
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
        multipleApps.Email = "multipleapps@example.com";
        _households["multipleapps@example.com"] = multipleApps;

        _logger.LogInformation("Seeded {Count} mock household records using Bogus", _households.Count);
    }

    /// <summary>
    /// Creates a defensive copy of household data to prevent external mutations.
    /// </summary>
    /// <param name="source">The source household data to copy.</param>
    /// <param name="includeAddress">Whether to include address information in the copy.</param>
    /// <returns>A new instance of HouseholdData with copied values.</returns>
    private static HouseholdData CreateCopy(HouseholdData source, bool includeAddress)
    {
        return new HouseholdData
        {
            Email = source.Email,
            Phone = source.Phone,
            BenefitIssuanceType = source.BenefitIssuanceType,
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
                IssuanceType = a.IssuanceType,
                Children = a.Children.Select(c => new Child
                {
                    FirstName = c.FirstName,
                    LastName = c.LastName
                }).ToList()
            }).ToList(),
            // Only include address if requested (simulating ID verification check)
            AddressOnFile = includeAddress && source.AddressOnFile != null
                ? new Address
                {
                    StreetAddress1 = source.AddressOnFile.StreetAddress1,
                    StreetAddress2 = source.AddressOnFile.StreetAddress2,
                    City = source.AddressOnFile.City,
                    State = source.AddressOnFile.State,
                    PostalCode = source.AddressOnFile.PostalCode
                }
                : null
        };
    }
}
