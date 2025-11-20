namespace SEBT.Portal.Core.Models.Results;
/// <summary>
/// Result of the RequestOtpCommand.
/// </summary>
public record RequestOtpCommandResult 
{
    /// <summary>
    /// Reference ID for the requested OTP.
    /// </summary>
    public required string OtpReference { get; init; }
}