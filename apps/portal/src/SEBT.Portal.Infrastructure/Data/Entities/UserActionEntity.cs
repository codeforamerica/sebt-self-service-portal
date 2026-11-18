using SEBT.Portal.Core.Models.UserActions;

namespace SEBT.Portal.Infrastructure.Data.Entities;

/// <summary>
/// An action a user sent to the state's system of record, recorded once per case, with its outcome.
/// </summary>
public class UserActionEntity
{
    /// <summary>
    /// The unique identifier of the user action.
    /// </summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// The unique identifier of the user who performed the action.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Navigation property to the user.
    /// </summary>
    public UserEntity? User { get; set; }

    /// <summary>
    /// The type of action the user took.
    /// </summary>
    public UserActionType Action { get; set; }

    /// <summary>
    /// The outcome of the user action.
    /// </summary>
    public UserActionOutcome Outcome { get; set; }

    /// <summary>
    /// AES-GCM ciphertext of <see cref="Core.Models.Household.SummerEbtCase.SummerEBTCaseID"/>.
    /// </summary>
    public string CaseId { get; set; } = string.Empty;

    /// <summary>
    /// HMAC-SHA256 of the case id. 64-character hex from <see cref="Core.Services.IIdentifierHasher"/>.
    /// </summary>
    public string CaseIdHash { get; set; } = string.Empty;

    /// <summary>
    /// AES-GCM ciphertext of the application id when the case is application-based.
    /// </summary>
    public string? ApplicationId { get; set; }

    /// <summary>
    /// HMAC-SHA256 of the application id, or empty when the case has none.
    /// </summary>
    public string ApplicationIdHash { get; set; } = string.Empty;

    /// <summary>
    /// AES-GCM ciphertext of the per-child application student id when present.
    /// </summary>
    public string? ApplicationStudentId { get; set; }

    /// <summary>
    /// HMAC-SHA256 of the application student id, or empty when the case has none.
    /// </summary>
    public string ApplicationStudentIdHash { get; set; } = string.Empty;

    /// <summary>
    /// OpenTelemetry span ID of the request, matching <c>span_id</c> in the portal's logs.
    /// Null when no trace is active, such as in a background job or test.
    /// </summary>
    public string? SpanId { get; set; }

    /// <summary>
    /// OpenTelemetry trace ID of the request, for linking to traces and log correlation.
    /// Null when no trace is active, such as in a background job or test.
    /// </summary>
    public string? TraceId { get; set; }

    /// <summary>
    /// UTC time the action's outcome was recorded.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
