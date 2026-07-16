using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Infrastructure.Repositories;
using SEBT.Portal.Api.Composition.Defaults;
using PluginAddress = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.Address;
using PluginAddressUpdateRequest = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.AddressUpdateRequest;

namespace SEBT.Portal.Tests.Unit.Api.Composition.Defaults;

public class MockStateAddressUpdateServiceTests
{
    private static readonly PiiVisibility FullPiiVisibility = new(IncludeAddress: true, IncludeEmail: true, IncludePhone: true);
    private static readonly DateTimeOffset FixedSeedTime = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly MockHouseholdRepository _mockRepo;
    private readonly MockStateAddressUpdateService _service;

    public MockStateAddressUpdateServiceTests()
    {
        var logger = NullLogger<MockHouseholdRepository>.Instance;
        _mockRepo = new MockHouseholdRepository(logger, timeProvider: new FakeTimeProvider(FixedSeedTime));
        _service = new MockStateAddressUpdateService(_mockRepo);
    }

    [Fact]
    public async Task UpdateAddressAsync_ReturnsSuccess()
    {
        var request = new PluginAddressUpdateRequest
        {
            HouseholdIdentifierValue = "verified@example.com",
            Address = new PluginAddress
            {
                StreetAddress1 = "42 Test Street",
                City = "TestCity",
                State = "DC",
                PostalCode = "20001"
            }
        };

        var result = await _service.UpdateAddressAsync(request);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task UpdateAddressAsync_UpdatesHouseholdAddressOnFile()
    {
        var request = new PluginAddressUpdateRequest
        {
            HouseholdIdentifierValue = "verified@example.com",
            Address = new PluginAddress
            {
                StreetAddress1 = "42 Test Street",
                StreetAddress2 = "Apt 1",
                City = "TestCity",
                State = "DC",
                PostalCode = "20001"
            }
        };

        await _service.UpdateAddressAsync(request);

        var household = await _mockRepo.GetHouseholdByEmailAsync(
            "verified@example.com", FullPiiVisibility, UserIalLevel.IAL1plus);
        Assert.NotNull(household?.AddressOnFile);
        Assert.Equal("42 Test Street", household.AddressOnFile.StreetAddress1);
        Assert.Equal("Apt 1", household.AddressOnFile.StreetAddress2);
        Assert.Equal("TestCity", household.AddressOnFile.City);
        Assert.Equal("DC", household.AddressOnFile.State);
        Assert.Equal("20001", household.AddressOnFile.PostalCode);
    }

    [Fact]
    public async Task UpdateAddressAsync_WhenHouseholdNotFound_StillReturnsSuccess()
    {
        // Mock service returns success even if household isn't found — the real
        // connector would have its own validation, and the handler already enforces
        // that the household exists before calling the state service.
        var request = new PluginAddressUpdateRequest
        {
            HouseholdIdentifierValue = "nonexistent@example.com",
            Address = new PluginAddress
            {
                StreetAddress1 = "1 Nowhere",
                City = "Nowhere",
                State = "XX",
                PostalCode = "00000"
            }
        };

        var result = await _service.UpdateAddressAsync(request);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task UpdateAddressAsync_UpdatesCaseMailingAddresses()
    {
        // Large-family scenario has 4 cases
        var request = new PluginAddressUpdateRequest
        {
            HouseholdIdentifierValue = "largefamily@example.com",
            Address = new PluginAddress
            {
                StreetAddress1 = "999 New Lane",
                City = "NewCity",
                State = "CO",
                PostalCode = "80000"
            }
        };

        await _service.UpdateAddressAsync(request);

        var household = await _mockRepo.GetHouseholdByEmailAsync(
            "largefamily@example.com", FullPiiVisibility, UserIalLevel.IAL1plus);
        Assert.NotNull(household);
        Assert.All(household.SummerEbtCases, c =>
        {
            Assert.NotNull(c.MailingAddress);
            Assert.Equal("999 New Lane", c.MailingAddress.StreetAddress1);
        });
    }
}
