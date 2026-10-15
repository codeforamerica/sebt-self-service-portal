using SEBT.Portal.Core.StateBackends;

namespace SEBT.Portal.Api.Composition;

internal static class FeatureGatedWriteResultMapper
{
    public static T Map<T>(
        WriteResult result,
        Func<T> success,
        Func<string, string, T> policyRejected,
        Func<string, string, T> backendError)
    {
        if (result.IsSuccess)
        {
            return success();
        }

        string code = result.ErrorCode ?? "BACKEND_ERROR";
        string message = result.ErrorMessage ?? "State backend request failed.";
        return result.IsPolicyRejection
            ? policyRejected(code, message)
            : backendError(code, message);
    }
}
