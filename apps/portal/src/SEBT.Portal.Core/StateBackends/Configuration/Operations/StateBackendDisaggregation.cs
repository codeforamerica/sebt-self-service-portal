namespace SEBT.Portal.Core.StateBackends.Configuration.Operations;

/// <summary>
/// Closed vocabulary for deciding whether a record represents an application-based case.
/// Deliberately NOT an expression DSL (DC-568 spike constraint).
/// </summary>
public enum DisaggregationRule
{
    /// <summary>
    /// A record is application-based when its <see cref="StateBackendDisaggregation.DiscriminatorField"/>
    /// is present (non-null / non-empty).
    /// </summary>
    Presence,

    /// <summary>
    /// A record is application-based when its <see cref="StateBackendDisaggregation.DiscriminatorField"/>
    /// value is in <see cref="StateBackendDisaggregation.ApplicationValues"/>.
    /// </summary>
    ValueInSet,
}

/// <summary>
/// Named case-inclusion predicates. A closed enum, not an expression language: each name
/// maps to a specific, code-owned predicate. New states requiring a new predicate must add
/// a named member here rather than expressing arbitrary logic in config.
/// </summary>
public enum CaseInclusionPredicate
{
    /// <summary>Include every record.</summary>
    All,

    /// <summary>Include a record when it is approved, or when it is not application-based.</summary>
    WhenApprovedOrNotApplicationBased,
}

/// <summary>
/// Disaggregation primitive: how to group records into applications and which records to
/// include as cases. Capped vocabulary per the DC-568 prototype plan.
/// </summary>
public sealed record StateBackendDisaggregation
{
    /// <summary>The rule used to classify a record as application-based.</summary>
    public required DisaggregationRule Rule { get; init; }

    /// <summary>Source field inspected by <see cref="Rule"/>.</summary>
    public required string DiscriminatorField { get; init; }

    /// <summary>Source field whose value groups records belonging to the same application.</summary>
    public string? GroupApplicationsBy { get; init; }

    /// <summary>
    /// For <see cref="DisaggregationRule.ValueInSet"/>: the discriminator values that mean
    /// "application-based".
    /// </summary>
    public List<string>? ApplicationValues { get; init; }

    /// <summary>The named predicate deciding which records are included as cases.</summary>
    public required CaseInclusionPredicate CaseInclusion { get; init; }
}
