using Microsoft.Extensions.Caching.Memory;
using Sebt.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Repositories;

namespace SEBT.Portal.Infrastructure.Repositories
{
    /// <summary>
    /// An in-memory implementation of <see cref="IOtpRepository"/> that uses <see cref="IMemoryCache"/> for storing OTP codes.
    /// </summary>
    /// <param name="memoryCache">The memory cache instance used to store and retrieve OTP codes.</param>
    /// <remarks>
    /// This implementation is suitable for single-instance applications. For distributed scenarios,
    /// consider using a distributed cache implementation instead.
    /// </remarks>
    public class InMemoryOtpRepository(IMemoryCache memoryCache) : IOtpRepository
    {
        public Task SaveOtpCodeAsync(OtpCode otpCode)
        {
            var existingCode = memoryCache.Get<OtpCode>(otpCode.Email);
            if (existingCode != null)
            {
                // If there's an existing valid OTP, do not overwrite it
                return Task.CompletedTask;
            }

            memoryCache.Set(otpCode.Email, otpCode, otpCode.ExpiresAt);
            return Task.CompletedTask;
        }

        public Task<OtpCode?> GetOtpCodeByEmailAsync(string email)
        {
            var otpCode = memoryCache.Get<OtpCode>(email);

            return Task.FromResult(otpCode);
        }

        public Task DeleteOtpCodeByEmailAsync(string email)
        {
            memoryCache.Remove(email);

            return Task.CompletedTask;
        }
    }
}