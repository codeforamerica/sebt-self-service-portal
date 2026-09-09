using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Tests.Unit.Infrastructure.Services;

public class CsvBlockedAddressDataSourceTests
{
    private const string Header = "OFF_ADR_L1__c,OFF_ADR_L2__c,OFF_ADR_CTY__c,OFF_ADR_PO_BOX__c,OFF_ADR_STA__c,OFF_ADR_ZIP__c";

    [Fact]
    public void GetEntries_ReturnsEmpty_WhenCsvHasOnlyHeader()
    {
        var source = CsvBlockedAddressDataSource.FromCsv(Header + "\n");

        Assert.Empty(source.GetEntries());
    }

    [Fact]
    public void GetEntries_ParsesL1Row_WithFiveDigitZip()
    {
        var csv = Header + "\n1575  SHERMAN ST,,DENVER,,CO,80203\n";

        var entries = CsvBlockedAddressDataSource.FromCsv(csv).GetEntries();

        var entry = Assert.Single(entries);
        Assert.Equal("1575  SHERMAN ST", entry.Street);
        Assert.Equal("80203", entry.PostalCodeFive);
    }

    [Fact]
    public void GetEntries_StripsZipPlusFour()
    {
        var csv = Header + "\n1575  SHERMAN ST,,DENVER,,CO,80203-1702\n";

        var entries = CsvBlockedAddressDataSource.FromCsv(csv).GetEntries();

        Assert.Equal("80203", Assert.Single(entries).PostalCodeFive);
    }

    [Fact]
    public void GetEntries_PoBoxOnlyRow_SynthesizesPoBoxStreet()
    {
        var csv = Header + "\n,,FORT MORGAN,220,CO,80701-0220\n";

        var entries = CsvBlockedAddressDataSource.FromCsv(csv).GetEntries();

        var entry = Assert.Single(entries);
        Assert.Equal("PO BOX 220", entry.Street);
        Assert.Equal("80701", entry.PostalCodeFive);
    }

    [Fact]
    public void GetEntries_RowWithBothL1AndPoBox_PrefersL1()
    {
        var csv = Header + "\n100 Main St,,Anytown,42,CO,80123\n";

        var entry = Assert.Single(CsvBlockedAddressDataSource.FromCsv(csv).GetEntries());

        Assert.Equal("100 Main St", entry.Street);
    }

    [Fact]
    public void GetEntries_DropsRow_WhenBothL1AndPoBoxAreEmpty()
    {
        var csv = Header + "\n,,Anytown,,CO,80123\n";

        Assert.Empty(CsvBlockedAddressDataSource.FromCsv(csv).GetEntries());
    }

    [Fact]
    public void GetEntries_DropsRow_WhenZipHasFewerThanFiveDigits()
    {
        var csv = Header + "\n100 Main St,,Anytown,,CO,808\n";

        Assert.Empty(CsvBlockedAddressDataSource.FromCsv(csv).GetEntries());
    }

    [Fact]
    public void GetEntries_SkipsBlankLines()
    {
        var csv = Header + "\n\n100 Main St,,Anytown,,CO,80123\n\n";

        Assert.Single(CsvBlockedAddressDataSource.FromCsv(csv).GetEntries());
    }

    [Fact]
    public void GetEntries_DropsRowsWithFewerThanSixFields()
    {
        var csv = Header + "\n100 Main St,,Anytown,80123\n";

        Assert.Empty(CsvBlockedAddressDataSource.FromCsv(csv).GetEntries());
    }

    [Fact]
    public void GetEntries_ParsesMultipleRows()
    {
        var csv = Header
            + "\n1575  SHERMAN ST,,DENVER,,CO,80203-1702"
            + "\n,,FORT MORGAN,220,CO,80701-0220"
            + "\n104 W 11TH ST,,DELTA,,CO,81416-1810\n";

        var entries = CsvBlockedAddressDataSource.FromCsv(csv).GetEntries();

        Assert.Equal(3, entries.Count);
    }

    [Fact]
    public void Constructor_WithMissingResource_ReturnsEmpty()
    {
        var assembly = typeof(CsvBlockedAddressDataSource).Assembly;

        var source = new CsvBlockedAddressDataSource(assembly, "SEBT.Portal.Infrastructure.NoSuchResource.csv");

        Assert.Empty(source.GetEntries());
    }

    [Fact]
    public void Constructor_WithEmbeddedCoCsv_LoadsKnownDenverEntry()
    {
        // The CO undeliverable-address CSV ships as an embedded resource in
        // SEBT.Portal.Infrastructure.csproj. This test pins down that the wiring
        // (file path, resource name, parser) is consistent end-to-end.
        var assembly = typeof(CsvBlockedAddressDataSource).Assembly;

        var source = new CsvBlockedAddressDataSource(
            assembly,
            "SEBT.Portal.Infrastructure.BlockedAddresses.co-undeliverable-addresses.csv");

        var entries = source.GetEntries();
        Assert.NotEmpty(entries);
        Assert.Contains(entries, e =>
            e.PostalCodeFive == "80203" &&
            e.Street.Contains("SHERMAN", StringComparison.OrdinalIgnoreCase));
    }
}
