using System.Reflection;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Services;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Loads blocked-address entries from a CSV embedded as an assembly resource.
/// Expected CSV columns (Salesforce export shape):
/// OFF_ADR_L1__c, OFF_ADR_L2__c, OFF_ADR_CTY__c, OFF_ADR_PO_BOX__c, OFF_ADR_STA__c, OFF_ADR_ZIP__c.
///
/// Rows with a Line 1 value yield an entry with that street; rows with only a PO box
/// number synthesize a "PO BOX {n}" street. ZIP+4 input is reduced to the 5-digit
/// prefix; rows with fewer than 5 digits in the ZIP column are dropped.
///
/// The parser is a pragmatic comma-split rather than a full RFC 4180 implementation:
/// the upstream Salesforce export does not quote fields or include embedded commas.
/// If that ever changes, switch to a CSV library before extending this class.
/// </summary>
public sealed class CsvBlockedAddressDataSource : IBlockedAddressDataSource
{
    private readonly IReadOnlyList<BlockedAddressEntry> _entries;

    public CsvBlockedAddressDataSource(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            _entries = [];
            return;
        }

        using var reader = new StreamReader(stream);
        _entries = ParseEntries(reader).ToList();
    }

    private CsvBlockedAddressDataSource(IReadOnlyList<BlockedAddressEntry> entries)
    {
        _entries = entries;
    }

    /// <summary>
    /// Test-friendly constructor that parses CSV content from an in-memory string
    /// rather than an assembly resource.
    /// </summary>
    public static CsvBlockedAddressDataSource FromCsv(string csvContent)
    {
        using var reader = new StringReader(csvContent);
        return new CsvBlockedAddressDataSource(ParseEntries(reader).ToList());
    }

    public IReadOnlyCollection<BlockedAddressEntry> GetEntries() => _entries;

    private static IEnumerable<BlockedAddressEntry> ParseEntries(TextReader reader)
    {
        var isHeader = true;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (isHeader)
            {
                isHeader = false;
                continue;
            }

            if (string.IsNullOrWhiteSpace(line)) continue;

            var fields = line.Split(',');
            if (fields.Length < 6) continue;

            var l1 = fields[0].Trim();
            var poBox = fields[3].Trim();
            var zip = fields[5].Trim();

            var zip5 = ExtractZipFive(zip);
            if (zip5 is null) continue;

            string street;
            if (!string.IsNullOrEmpty(l1))
            {
                street = l1;
            }
            else if (!string.IsNullOrEmpty(poBox))
            {
                street = $"PO BOX {poBox}";
            }
            else
            {
                continue;
            }

            yield return new BlockedAddressEntry(street, zip5);
        }
    }

    private static string? ExtractZipFive(string zip)
    {
        Span<char> digits = stackalloc char[5];
        var written = 0;
        foreach (var ch in zip)
        {
            if (!char.IsDigit(ch)) continue;
            digits[written++] = ch;
            if (written == 5) return new string(digits);
        }

        return null;
    }
}
