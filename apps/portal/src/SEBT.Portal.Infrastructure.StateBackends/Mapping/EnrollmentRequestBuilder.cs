using System.Globalization;
using System.Text.Json.Nodes;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.Core.StateBackends.Configuration.Operations;

namespace SEBT.Portal.Infrastructure.StateBackends.Mapping;

/// <summary>
/// Builds the enrollment op's outgoing request-row array from the child batch: one row per child
/// tagged with a 1-based correlation index, plus a second DOB-expanded row under the same index
/// when the binding's <see cref="EnrollmentRequestBinding.Expand"/> strategy applies.
/// </summary>
internal static class EnrollmentRequestBuilder
{
    // Closed set of child fields a backend match reads.
    private const string FirstNameInput = "firstName";
    private const string LastNameInput = "lastName";
    private const string DobInput = "dob";
    private const string SchoolIdentifierInput = "schoolIdentifier";

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

            // The swapped-DOB candidate goes under the same index.
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
        BindChildFields(body, binding, child, child.DateOfBirth);
        return body;
    }

    private static JsonObject BuildRow(
        EnrollmentRequestBinding binding, EnrollmentChild child, DateOnly dob, string indexField, string index)
    {
        var row = new JsonObject();
        BindChildFields(row, binding, child, dob);
        JsonPathWriter.Write(row, indexField, JsonValue.Create(index));
        return row;
    }

    // Required entries fail loud on an unresolved input; optional entries are omitted instead.
    private static void BindChildFields(
        JsonObject target, EnrollmentRequestBinding binding, EnrollmentChild child, DateOnly dob)
    {
        foreach ((string inputName, string targetPath) in binding.Map)
        {
            JsonPathWriter.Write(target, targetPath, JsonValue.Create(ResolveInput(inputName, child, dob)));
        }

        if (binding.MapOptional is { } mapOptional)
        {
            foreach ((string inputName, string targetPath) in mapOptional)
            {
                if (TryResolveInput(inputName, child, dob) is { } value)
                {
                    JsonPathWriter.Write(target, targetPath, JsonValue.Create(value));
                }
            }
        }
    }

    // A map input with no value — an unknown name, or a nullable child field the child doesn't
    // carry — fails loud rather than silently dropping. mapOptional is the omit-instead channel.
    private static string ResolveInput(string inputName, EnrollmentChild child, DateOnly dob) =>
        TryResolveInput(inputName, child, dob)
            ?? throw new InvalidOperationException(
                $"Enrollment request map input '{inputName}' resolved to no value.");

    private static string? TryResolveInput(string inputName, EnrollmentChild child, DateOnly dob) =>
        inputName switch
        {
            FirstNameInput => child.FirstName,
            LastNameInput => child.LastName,
            DobInput => dob.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            SchoolIdentifierInput => child.SchoolIdentifier,
            _ => null,
        };
}
