namespace SEBT.Portal.Core.StateBackends.Configuration.Operations;

/// <summary>
/// Names the source properties that compose a case mailing address.
/// </summary>
public sealed record MailingAddressMapping
{
    public string? Line1 { get; init; }

    public string? Line2 { get; init; }

    public string? City { get; init; }

    public string? State { get; init; }

    public string? Zip { get; init; }

    public string? Zip4 { get; init; }
}
