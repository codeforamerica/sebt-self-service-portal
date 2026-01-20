using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Infrastructure.Helpers;

namespace SEBT.Portal.Infrastructure.Repositories;

/// <summary>
/// Mock implementation of household repository for development and testing.
/// Returns mock data without requiring a database or external service.
/// </summary>
public class MockHouseholdRepository : IHouseholdRepository
{
    private readonly Dictionary<string, HouseholdData> _households;
    private readonly ILogger<MockHouseholdRepository> _logger;

    public MockHouseholdRepository(ILogger<MockHouseholdRepository> logger)
    {
        _logger = logger;
        _households = new Dictionary<string, HouseholdData>();
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

        var normalizedEmail = NormalizeEmail(email);
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

        var normalizedEmail = NormalizeEmail(householdData.Email);

        // Create a defensive copy to prevent external mutations
        var copy = CreateCopy(householdData, includeAddress: true);
        _households[normalizedEmail] = copy;

        _logger.LogInformation("Mock household data updated for email {Email}", normalizedEmail);
        return Task.CompletedTask;
    }

    private void SeedMockData()
    {
        // Scenario 1: Approved application with address (ID verified user)
        _households["verified@example.com"] = HouseholdFactory.CreateHouseholdDataWithEmail("verified@example.com", h =>
        {
            h.Phone = "555-1234";
            h.Children = new List<Child>
            {
                new Child { FirstName = "John", LastName = "Doe" },
                new Child { FirstName = "Jane", LastName = "Doe" }
            };
            h.BenefitIssueDate = DateTime.UtcNow.AddDays(-30);
            h.BenefitExpirationDate = DateTime.UtcNow.AddDays(60);
            h.Last4DigitsOfCard = "1234";
            h.ApplicationNumber = "APP-2024-001234";
            h.CaseNumber = "CASE-567890";
            h.ApplicationStatus = ApplicationStatus.Approved;
            h.AddressOnFile = new Address
            {
                StreetAddress1 = "123 Main Street",
                StreetAddress2 = "Apt 4B",
                City = "Denver",
                State = "CO",
                PostalCode = "80202"
            };
        });

        // Scenario 2: Pending application without address (not ID verified)
        _households["pending@example.com"] = HouseholdFactory.CreateHouseholdDataWithEmail("pending@example.com", h =>
        {
            h.Phone = "555-5678";
            h.Children = new List<Child>
            {
                new Child { FirstName = "Alice", LastName = "Smith" }
            };
            h.ApplicationStatus = ApplicationStatus.Pending;
            h.ApplicationNumber = "APP-2024-005678";
            h.AddressOnFile = new Address
            {
                StreetAddress1 = "456 Oak Avenue",
                City = "Boulder",
                State = "CO",
                PostalCode = "80301"
            };
        });

        // Scenario 3: Denied application
        _households["denied@example.com"] = HouseholdFactory.CreateHouseholdDataWithEmail("denied@example.com", h =>
        {
            h.Phone = "555-9999";
            h.ApplicationStatus = ApplicationStatus.Denied;
            h.ApplicationNumber = "APP-2024-009999";
            h.CaseNumber = "CASE-999999";
            h.Children = new List<Child>();
        });

        // Scenario 4: Under review
        _households["review@example.com"] = HouseholdFactory.CreateHouseholdDataWithEmail("review@example.com", h =>
        {
            h.Phone = "555-1111";
            h.ApplicationStatus = ApplicationStatus.UnderReview;
            h.ApplicationNumber = "APP-2024-001111";
            h.Children = new List<Child>
            {
                new Child { FirstName = "Bob", LastName = "Johnson" }
            };
        });

        // Scenario 5: Cancelled application
        _households["cancelled@example.com"] = HouseholdFactory.CreateHouseholdDataWithEmail("cancelled@example.com", h =>
        {
            h.Phone = "555-2222";
            h.ApplicationStatus = ApplicationStatus.Cancelled;
            h.ApplicationNumber = "APP-2024-002222";
            h.Children = new List<Child>();
        });

        // Scenario 6: Approved with single child
        _households["singlechild@example.com"] = HouseholdFactory.CreateHouseholdDataWithEmail("singlechild@example.com", h =>
        {
            h.Phone = "555-3333";
            h.Children = new List<Child>
            {
                new Child { FirstName = "Emma", LastName = "Williams" }
            };
            h.BenefitIssueDate = DateTime.UtcNow.AddDays(-15);
            h.BenefitExpirationDate = DateTime.UtcNow.AddDays(75);
            h.Last4DigitsOfCard = "5678";
            h.ApplicationNumber = "APP-2024-003333";
            h.CaseNumber = "CASE-333333";
            h.ApplicationStatus = ApplicationStatus.Approved;
            h.AddressOnFile = new Address
            {
                StreetAddress1 = "789 Pine Street",
                City = "Colorado Springs",
                State = "CO",
                PostalCode = "80901"
            };
        });

        // Scenario 7: Large family (multiple children)
        _households["largefamily@example.com"] = HouseholdFactory.CreateHouseholdDataWithEmail("largefamily@example.com", h =>
        {
            h.Phone = "555-4444";
            h.Children = new List<Child>
            {
                new Child { FirstName = "Michael", LastName = "Brown" },
                new Child { FirstName = "Sarah", LastName = "Brown" },
                new Child { FirstName = "David", LastName = "Brown" },
                new Child { FirstName = "Emily", LastName = "Brown" }
            };
            h.BenefitIssueDate = DateTime.UtcNow.AddDays(-45);
            h.BenefitExpirationDate = DateTime.UtcNow.AddDays(45);
            h.Last4DigitsOfCard = "9012";
            h.ApplicationNumber = "APP-2024-004444";
            h.CaseNumber = "CASE-444444";
            h.ApplicationStatus = ApplicationStatus.Approved;
            h.AddressOnFile = new Address
            {
                StreetAddress1 = "321 Elm Street",
                StreetAddress2 = "Unit 2",
                City = "Fort Collins",
                State = "CO",
                PostalCode = "80521"
            };
        });

        // Scenario 8: Minimal data (no phone, no dates)
        _households["minimal@example.com"] = HouseholdFactory.CreateHouseholdDataWithEmail("minimal@example.com", h =>
        {
            h.Phone = null;
            h.ApplicationStatus = ApplicationStatus.Pending;
            h.Children = new List<Child>();
        });

        // Scenario 9: Expired benefits
        _households["expired@example.com"] = HouseholdFactory.CreateHouseholdDataWithEmail("expired@example.com", h =>
        {
            h.Phone = "555-5555";
            h.Children = new List<Child>
            {
                new Child { FirstName = "Olivia", LastName = "Davis" }
            };
            h.BenefitIssueDate = DateTime.UtcNow.AddDays(-120);
            h.BenefitExpirationDate = DateTime.UtcNow.AddDays(-10); // Expired
            h.Last4DigitsOfCard = "3456";
            h.ApplicationNumber = "APP-2023-005555";
            h.CaseNumber = "CASE-555555";
            h.ApplicationStatus = ApplicationStatus.Approved;
            h.AddressOnFile = new Address
            {
                StreetAddress1 = "654 Maple Drive",
                City = "Aurora",
                State = "CO",
                PostalCode = "80012"
            };
        });

        // Scenario 10: Unknown status
        _households["unknown@example.com"] = HouseholdFactory.CreateHouseholdDataWithEmail("unknown@example.com", h =>
        {
            h.Phone = "555-6666";
            h.ApplicationStatus = ApplicationStatus.Unknown;
            h.Children = new List<Child>();
        });

        _logger.LogInformation("Seeded {Count} mock household records", _households.Count);
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
            BenefitIssueDate = source.BenefitIssueDate,
            BenefitExpirationDate = source.BenefitExpirationDate,
            Last4DigitsOfCard = source.Last4DigitsOfCard,
            ApplicationNumber = source.ApplicationNumber,
            CaseNumber = source.CaseNumber,
            ApplicationStatus = source.ApplicationStatus,
            Children = source.Children.Select(c => new Child
            {
                FirstName = c.FirstName,
                LastName = c.LastName
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

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }
}
