namespace SEBT.Portal.Core.Repositories
{
    using Sebt.Portal.Core.Models.Auth;
    /// <summary>
    /// Repository interface for managing OTP (One-Time Password) codes.
    /// </summary>
    public interface IOtpRepository
    {
        /// <summary>
        /// Saves the provided OTP code to the repository.
        /// </summary>
        /// <param name="otpCode">The OTP code to save.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SaveOtpCodeAsync(OtpCode otpCode);

        /// <summary>
        /// Retrieves the OTP code associated with the specified email.
        /// </summary>
        /// <param name="email">The email address associated with the OTP code.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the OTP code if found; otherwise, <c>null</c>.</returns>
        Task<OtpCode?> GetOtpCodeByEmailAsync(string email);

        /// <summary>
        /// Deletes the OTP code associated with the specified email.
        /// </summary>
        /// <param name="email">The email address associated with the OTP code to delete.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task DeleteOtpCodeByEmailAsync(string email);
    }
}