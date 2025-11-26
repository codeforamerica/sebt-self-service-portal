using Microsoft.Extensions.Caching.Memory;
using Sebt.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Repositories;

public class InMemoryOtpRepository(IMemoryCache memoryCache) : IOtpRepository
{
    private readonly Dictionary<string, OtpCode> _store = new();

    public Task SaveOtpCodeAsync(OtpCode otpCode)
    {
        var existingCode = memoryCache.Get<OtpCode>(otpCode.Email);
        if (existingCode != null)
        {
            // If there's an existing valid OTP, do not overwrite it
            return Task.CompletedTask;
        }
        else
        {   
            memoryCache.Set(otpCode.Email, otpCode, otpCode.ExpiresAt);
            return Task.CompletedTask;
        }
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