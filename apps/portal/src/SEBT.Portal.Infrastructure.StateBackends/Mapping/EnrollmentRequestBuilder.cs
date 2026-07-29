using System.Globalization;
using System.Text.Json.Nodes;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.Core.StateBackends.Configuration.Operations;

namespace SEBT.Portal.Infrastructure.StateBackends.Mapping;

/// <summary>
/// Builds the enrollment op's outgoing request-row array from the child batch (DC-568 spike).
/// Each child yields one row built from the binding's <c>map</c> (OUR child field → dotted target
/// path) tagged with a 1-based correlation index at <see cref="EnrollmentRequestBinding.IndexField"/>.
/// When the binding's <see cref="EnrollmentRequestBinding.Expand"/> strategy is
/// <see cref="CandidateExpansion.TransposeMonthDay"/> and the DOB is transposable, a SECOND row is
/// emitted under the SAME index — the request-side candidate-expansion brick. The correlation index
/// lets the response correlator fan candidate verdicts back into one per-child outcome.
/// </summary>
internal static class EnrollmentRequestBuilder
{
    // Closed LHS of the enrollment request map: the child fields a backend match reads.
    private const string FirstNameInput = "firstName";
    private const string LastNameInput = "lastName";
    private const string DobInput = "dob";

    public static JsonArray BuildRows(EnrollmentRequestBinding binding, EnrollmentCheckRequest request)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(request);

        // Batch mode: the validator guarantees a non-null index field before we get here.
        string indexField = binding.IndexField
            ?? throw new InvalidOperationException("Batch enrollment request binding requires an indexField.");

        var rows = new JsonArray();

        for (int i = 0; i < request.Children.Count; i++)
        {
            EnrollmentChild child = request.Children[i];
            string index = (i + 1).ToString(CultureInfo.InvariantCulture);

            rows.Add(BuildRow(binding, child, child.DateOfBirth, indexField, index));

            // Candidate expansion: emit the month/day-swapped DOB under the SAME index, but only
            // when the swap yields a valid AND different date.
            if (binding.Expand == CandidateExpansion.TransposeMonthDay
                && EnrollmentCandidateExpander.TryTransposeMonthDay(child.DateOfBirth) is { } transposed)
            {
                rows.Add(BuildRow(binding, child, transposed, indexField, index));
            }
        }

        return rows;
    }

    /// <summary>
    /// Builds a single child's request body for PerChild fan-out: the same <c>map</c> vocabulary as a
    /// batch row, but WITHOUT a correlation index (each call is one child).
    /// </summary>
    public static JsonObject BuildSingleChildBody(EnrollmentRequestBinding binding, EnrollmentChild child)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(child);

        var body = new JsonObject();

        foreach ((string inputName, string targetPath) in binding.Map)
        {
            JsonPathWriter.Write(body, targetPath, JsonValue.Create(ResolveInput(inputName, child, child.DateOfBirth)));
        }

        return body;
    }

    private static JsonObject BuildRow(
        EnrollmentRequestBinding binding, EnrollmentChild child, DateOnly dob, string indexField, string index)
    {
        var row = new JsonObject();

        foreach ((string inputName, string targetPath) in binding.Map)
        {
            JsonPathWriter.Write(row, targetPath, JsonValue.Create(ResolveInput(inputName, child, dob)));
        }

        JsonPathWriter.Write(row, indexField, JsonValue.Create(index));

        return row;
    }

    // Closed resolution set — the three child fields a backend match reads. A map input outside
    // this set fails loud rather than silently dropping.
    private static string ResolveInput(string inputName, EnrollmentChild child, DateOnly dob) =>
        inputName switch
        {
            FirstNameInput => child.FirstName,
            LastNameInput => child.LastName,
            DobInput => dob.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException(
                $"Enrollment request map input '{inputName}' is not a known child field."),
        };
}
