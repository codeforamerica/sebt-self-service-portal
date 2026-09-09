using DotNet.Testcontainers.Containers;
using NSubstitute;
using SEBT.Portal.Tests.Helpers;

namespace SEBT.Portal.Tests.Unit.Helpers;

public class ContainerStartupRetryTests
{
    [Fact]
    public async Task StartWithRetryAsync_SucceedsOnFirstAttempt_CallsStartOnce()
    {
        var container = Substitute.For<IContainer>();

        await container.StartWithRetryAsync(maxAttempts: 3, delay: TimeSpan.Zero);

        await container.Received(1).StartAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartWithRetryAsync_FailsThenSucceeds_RetriesUntilSuccessful()
    {
        var container = Substitute.For<IContainer>();
        var attempts = 0;
        container.StartAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            attempts++;
            return attempts < 2 ? Task.FromException(new InvalidOperationException("transient")) : Task.CompletedTask;
        });

        await container.StartWithRetryAsync(maxAttempts: 3, delay: TimeSpan.Zero);

        await container.Received(2).StartAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartWithRetryAsync_FailsEveryAttempt_ThrowsAfterMaxAttempts()
    {
        var container = Substitute.For<IContainer>();
        container.StartAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("permanent")));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => container.StartWithRetryAsync(maxAttempts: 3, delay: TimeSpan.Zero));

        await container.Received(3).StartAsync(Arg.Any<CancellationToken>());
    }
}
