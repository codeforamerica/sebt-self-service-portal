namespace SEBT.Portal.Core.StateBackends.Configuration.Operations;

/// <summary>Rule deciding whether a record represents an application-based case.</summary>
public enum DisaggregationRule
{
    /// <summary>Application-based when the <see cref="StateBackendDisaggregation.DiscriminatorField"/> is present.</summary>
    Presence,

    /// <summary>Application-based when the discriminator's value is in <see cref="StateBackendDisaggregation.ApplicationValues"/>.</summary>
    ValueInSet,
}

/// <summary>Named case-inclusion predicate deciding which records become cases.</summary>
public enum CaseInclusionPredicate
{
    /// <summary>Include every record.</summary>
    All,

    /// <summary>Include a record when it is approved, or when it is not application-based.</summary>
    WhenApprovedOrNotApplicationBased,
}

/// <summary>How to group records into applications and which records to include as cases.</summary>
public sealed record StateBackendDisaggregation
{
    /// <summary>The rule used to classify a record as application-based.</summary>
    public required DisaggregationRule Rule { get; init; }

    /// <summary>Source field inspected by <see cref="Rule"/>.</summary>
    public required string DiscriminatorField { get; init; }

    /// <summary>Source field whose value groups records belonging to the same application.</summary>
    public string? GroupApplicationsBy { get; init; }

    /// <summary>For <see cref="DisaggregationRule.ValueInSet"/>: the discriminator values that mean "application-based".</summary>
    public List<string>? ApplicationValues { get; init; }

    /// <summary>The named predicate deciding which records are included as cases.</summary>
    public required CaseInclusionPredicate CaseInclusion { get; init; }
}
