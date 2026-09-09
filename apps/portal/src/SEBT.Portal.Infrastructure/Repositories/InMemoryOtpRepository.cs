using Microsoft.Extensions.Caching.Memory;
using SEBT.Portal.Core.Models.Auth;
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
        public async Task SaveOtpCodeAsync(OtpCode newOtpCode)
        {
            var existingCode = await GetOtpCodeByEmailAsync(newOtpCode.Email);

            if (existingCode != null)
            {
                await DeleteOtpCodeByEmailAsync(newOtpCode.Email);
            }

            memoryCache.Set(newOtpCode.Email, newOtpCode, newOtpCode.ExpiresAt);
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
