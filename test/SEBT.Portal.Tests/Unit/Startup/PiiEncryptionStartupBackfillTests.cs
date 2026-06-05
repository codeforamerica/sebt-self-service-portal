using Microsoft.Extensions.Logging.Abstractions;
using SEBT.Portal.Api.Startup;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Exceptions;
using Xunit;

namespace SEBT.Portal.Tests.Unit.Startup;

public class PiiEncryptionStartupBackfillTests
{
    [Fact]
    public void ShouldRunStartupBackfill_WhenDefaultSettings_ReturnsFalse()
    {
        Assert.False(PiiEncryptionStartupBackfill.ShouldRunStartupBackfill(new PiiEncryptionSettings()));
    }

    [Fact]
    public void ShouldRunStartupBackfill_WhenExplicitlyTrue_ReturnsTrue()
    {
        Assert.True(PiiEncryptionStartupBackfill.ShouldRunStartupBackfill(
            new PiiEncryptionSettings { RunStartupBackfill = true }));
    }

    [Fact]
    public void ShouldRunStartupBackfill_WhenExplicitlyFalse_ReturnsFalse()
    {
        Assert.False(PiiEncryptionStartupBackfill.ShouldRunStartupBackfill(
            new PiiEncryptionSettings { RunStartupBackfill = false }));
    }

    [Fact]
    public void ShouldRunStartupBackfill_WhenSettingsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            PiiEncryptionStartupBackfill.ShouldRunStartupBackfill(null!));
    }

    [Fact]
    public async Task RunIfEnabledAsync_WhenDisabled_DoesNotInvokeBackfill()
    {
        var invoked = false;

        await PiiEncryptionStartupBackfill.RunIfEnabledAsync(
            new PiiEncryptionSettings { RunStartupBackfill = false },
            _ =>
            {
                invoked = true;
                return Task.CompletedTask;
            },
            NullLogger.Instance);

        Assert.False(invoked);
    }

    [Fact]
    public async Task RunIfEnabledAsync_WhenEnabled_InvokesBackfill()
    {
        var invoked = false;

        await PiiEncryptionStartupBackfill.RunIfEnabledAsync(
            new PiiEncryptionSettings { RunStartupBackfill = true },
            _ =>
            {
                invoked = true;
                return Task.CompletedTask;
            },
            NullLogger.Instance);

        Assert.True(invoked);
    }

    [Fact]
    public async Task RunIfEnabledAsync_WhenBackfillThrowsPiiDecryptException_DoesNotPropagate()
    {
        await PiiEncryptionStartupBackfill.RunIfEnabledAsync(
            new PiiEncryptionSettings { RunStartupBackfill = true },
            _ => throw new PiiDecryptException("tampered"),
            NullLogger.Instance);
    }

    [Fact]
    public async Task RunIfEnabledAsync_WhenBackfillThrowsUnexpectedException_DoesNotPropagate()
    {
        await PiiEncryptionStartupBackfill.RunIfEnabledAsync(
            new PiiEncryptionSettings { RunStartupBackfill = true },
            _ => throw new InvalidOperationException("transient"),
            NullLogger.Instance);
    }
}
