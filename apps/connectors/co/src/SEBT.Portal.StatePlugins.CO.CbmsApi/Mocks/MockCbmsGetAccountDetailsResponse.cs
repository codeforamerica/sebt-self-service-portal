using System.Text.Json;

namespace SEBT.Portal.StatePlugins.CO.CbmsApi.Mocks;

/// <summary>
/// Adjusts embedded get-account-details mock JSON to mirror CBMS <c>ebtCardService=N</c> responses.
/// </summary>
internal static class MockCbmsGetAccountDetailsResponse
{
    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly string[] CardFieldNames =
    [
        "ebtCardLastFour",
        "ebtCardSts",
        "cardIssDt",
        "cardBal"
    ];

    public static string WithoutCardFields(string json)
    {
        using var doc = JsonDocument.Parse(json, JsonOptions);
        if (!doc.RootElement.TryGetProperty("stdntEnrollDtls", out var students) || students.ValueKind != JsonValueKind.Array)
        {
            return json;
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (property.NameEquals("stdntEnrollDtls"))
                {
                    writer.WritePropertyName("stdntEnrollDtls");
                    writer.WriteStartArray();
                    foreach (var student in students.EnumerateArray())
                    {
                        writer.WriteStartObject();
                        foreach (var field in student.EnumerateObject())
                        {
                            if (CardFieldNames.Contains(field.Name, StringComparer.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            field.WriteTo(writer);
                        }

                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                    continue;
                }

                property.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }
}
