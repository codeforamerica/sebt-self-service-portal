using Microsoft.Extensions.Logging;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.UseCases.Diagnostics;

public class TestErrorCommandHandler(ILogger<TestErrorCommandHandler> logger)
    : ICommandHandler<TestErrorCommand>
{
    // Long enough that a 500 ms controller-side timeout reliably fires first.
    private const int SimulatedDelayMs = 30_000;

    public async Task<Result> Handle(TestErrorCommand command, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "TestErrorCommandHandler invoked (WithDelay={WithDelay}) — diagnostic endpoint",
            command.WithDelay);

        if (command.WithDelay)
        {
            // Awaiting Task.Delay with the caller's token produces a realistic OperationCanceledException
            // through the OTEL span stack when the controller cancels via its short-lived CTS.
            await Task.Delay(SimulatedDelayMs, cancellationToken);
        }

        throw new InvalidOperationException(
            "Test error: simulated unhandled exception for OTEL span and structured logging validation.");
    }
}
