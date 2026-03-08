namespace SEBT.Portal.Api.Models.IdProofing;

/// <summary>
/// Request body for POST /api/id-proofing.
/// Maps the frontend form submission to the use case command.
/// </summary>
public record SubmitIdProofingRequest(
    string DateOfBirth,
    string? IdType,
    string? IdValue);
