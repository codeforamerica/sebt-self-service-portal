namespace SEBT.Portal.Infrastructure.Services;

internal static class GeneralDeliveryDetection
{
    internal static bool IsGeneralDeliveryRecordType(string? recordType) =>
        string.Equals(recordType, "G", StringComparison.OrdinalIgnoreCase);

    internal static bool TextIndicatesGeneralDelivery(string? street1, string? street2)
    {
        var s1 = street1.AsSpan().Trim();
        var s2 = street2.AsSpan().Trim();
        return ContainsGeneralDelivery(s1) || ContainsGeneralDelivery(s2);
    }

    private static bool ContainsGeneralDelivery(ReadOnlySpan<char> line)
    {
        if (line.Trim().IsEmpty)
        {
            return false;
        }

        return line.ToString().Contains("general delivery", StringComparison.OrdinalIgnoreCase);
    }
}
