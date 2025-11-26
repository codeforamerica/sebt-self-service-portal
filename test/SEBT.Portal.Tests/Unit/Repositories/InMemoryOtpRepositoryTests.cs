using Microsoft.Extensions.Caching.Memory;
using Sebt.Portal.Core.Models.Auth;

namespace SEBT.Portal.Tests.Unit.Repositories;

public class InMemoryOtpRepositoryTests
{

    [Fact]
    public async Task SaveOtpCodeAsync_WhenNoExistingCode_ShouldStoreInCache()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var repo = new InMemoryOtpRepository(memoryCache);

        var otp = new OtpCode("123456", "test@example.com");

        await repo.SaveOtpCodeAsync(otp);

        var stored = await repo.GetOtpCodeByEmailAsync(otp.Email);

        Assert.NotNull(stored);
        Assert.Equal(otp.Email, stored!.Email);
        Assert.Equal(otp.Code, stored.Code);
        Assert.Equal(otp.ExpiresAt, stored.ExpiresAt);
    }

    [Fact]
    public async Task SaveOtpCodeAsync_WhenExistingCode_ShouldNotOverwrite()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var repo = new InMemoryOtpRepository(memoryCache);

        var existing = new OtpCode("0000000", "user@example.com");

        // seed existing code directly into the cache to simulate a valid existing OTP
        memoryCache.Set(existing.Email, existing, existing.ExpiresAt);

        var newOtp = new OtpCode("999999", existing.Email);

        await repo.SaveOtpCodeAsync(newOtp);

        var stored = await repo.GetOtpCodeByEmailAsync(existing.Email);

        Assert.NotNull(stored);
        // should still be the original code, not overwritten by newOtp
        Assert.Equal(existing.Code, stored!.Code);
        Assert.Equal(existing.ExpiresAt, stored.ExpiresAt);
    }

    [Fact]
    public async Task GetOtpCodeByEmailAsync_WhenCodeExists_ShouldReturnOtpCode()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var repo = new InMemoryOtpRepository(memoryCache);

        var otp = new OtpCode("567890", "existing@example.com");

        memoryCache.Set(otp.Email, otp, otp.ExpiresAt);

        var result = await repo.GetOtpCodeByEmailAsync(otp.Email);

        Assert.NotNull(result);
        Assert.Equal(otp.Email, result!.Email);
        Assert.Equal(otp.Code, result.Code);
        Assert.Equal(otp.ExpiresAt, result.ExpiresAt);
    }

    [Fact]
    public async Task GetOtpCodeByEmailAsync_WhenCodeDoesNotExist_ShouldReturnNull()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var repo = new InMemoryOtpRepository(memoryCache);

        var result = await repo.GetOtpCodeByEmailAsync("nonexistent@example.com");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetOtpCodeByEmailAsync_WhenCodeExpired_ShouldReturnNull()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var repo = new InMemoryOtpRepository(memoryCache);

        var otp = new OtpCode("111111", "expired@example.com", -1);

        memoryCache.Set(otp.Email, otp, otp.ExpiresAt);

        // Wait for the cache entry to expire
        await Task.Delay(50);

        var result = await repo.GetOtpCodeByEmailAsync(otp.Email);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetOtpCodeByEmailAsync_WithDifferentEmails_ShouldReturnCorrectCode()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var repo = new InMemoryOtpRepository(memoryCache);

        var otp1 = new OtpCode("111111", "user1@example.com");

        var otp2 = new OtpCode("222222", "user2@example.com");

        memoryCache.Set(otp1.Email, otp1, otp1.ExpiresAt);
        memoryCache.Set(otp2.Email, otp2, otp2.ExpiresAt);

        var result1 = await repo.GetOtpCodeByEmailAsync(otp1.Email);
        var result2 = await repo.GetOtpCodeByEmailAsync(otp2.Email);

        Assert.NotNull(result1);
        Assert.Equal("111111", result1!.Code);

        Assert.NotNull(result2);
        Assert.Equal("222222", result2!.Code);
    }
}
