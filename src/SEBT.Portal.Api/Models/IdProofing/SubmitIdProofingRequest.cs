namespace SEBT.Portal.Api.Models.IdProofing;

/// <summary>
/// Structured date-of-birth as sent by the frontend form.
/// Each component is zero-padded (e.g. month "03", day "15", year "1990").
/// </summary>
public record DateOfBirthDto(string Month, string Day, string Year);

/// <summary>
/// Request body for POST /api/id-proofing.
/// Maps the frontend form submission to the use case command.
/// </summary>
public record SubmitIdProofingRequest(
    DateOfBirthDto DateOfBirth,
    string? IdType,
    string? IdValue,
    string? DiSessionToken = null);
