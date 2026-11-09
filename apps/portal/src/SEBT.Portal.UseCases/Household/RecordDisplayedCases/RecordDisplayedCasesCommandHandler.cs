using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Utilities;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.UseCases.Household;

/// <summary>
/// Persists the cases displayed to a portal user so later operations can join back to that user.
/// Failures are reported as a result rather than thrown, so callers can treat recording as best-effort.
/// </summary>
public class RecordDisplayedCasesCommandHandler(
    IUserCaseRepository userCaseRepository,
    ILogger<RecordDisplayedCasesCommandHandler> logger)
    : ICommandHandler<RecordDisplayedCasesCommand>
{
    public async Task<Result> Handle(
        RecordDisplayedCasesCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.User.GetUserId() is not Guid userId)
        {
            return Result.Unauthorized("Unable to identify user from token.");
        }

        var displayedCases = ToIdentifiersWithCaseId(command);
        if (displayedCases.Count == 0)
        {
            return Result.Success();
        }

        try
        {
            await userCaseRepository.UpsertDisplayedCasesAsync(userId, displayedCases, cancellationToken);
            return Result.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to record displayed cases for user {UserId}", userId);
            return Result.DependencyFailed(
                DependencyFailedReason.ConnectionFailed,
                "Unable to record displayed cases.");
        }
    }

    private static List<UserCaseIdentifiers> ToIdentifiersWithCaseId(RecordDisplayedCasesCommand command)
    {
        var identifiers = new List<UserCaseIdentifiers>();
        foreach (var displayed in command.Cases)
        {
            var caseId = displayed.SummerEBTCaseID;
            if (string.IsNullOrWhiteSpace(caseId))
            {
                continue;
            }

            identifiers.Add(new UserCaseIdentifiers(
                caseId,
                displayed.ApplicationId,
                displayed.ApplicationStudentId));
        }

        return identifiers;
    }
}
