using Microsoft.Extensions.Logging.Abstractions;
using SEBT.Portal.UseCases.Diagnostics;

namespace SEBT.Portal.Tests.Unit.UseCases.Diagnostics;

public class TestErrorCommandHandlerTests
{
    private readonly TestErrorCommandHandler _handler = new(NullLogger<TestErrorCommandHandler>.Instance);

    [Fact]
    public async Task Handle_ThrowsSimulatedException()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(new TestErrorCommand(), CancellationToken.None));

        Assert.StartsWith("Test error:", exception.Message);
    }

    [Fact]
    public async Task Handle_WithDelay_HonorsCancellation()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _handler.Handle(new TestErrorCommand { WithDelay = true }, cts.Token));
    }
}
