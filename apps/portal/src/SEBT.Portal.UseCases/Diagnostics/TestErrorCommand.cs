using SEBT.Portal.Kernel;

namespace SEBT.Portal.UseCases.Diagnostics;

public class TestErrorCommand : ICommand
{
    /// <summary>
    /// When true, the handler delays 30 seconds before throwing, simulating a slow dependency.
    /// The caller is expected to cancel via a short-lived CancellationTokenSource to exercise
    /// the timeout / OperationCanceledException path in OTEL tracing.
    /// </summary>
    public bool WithDelay { get; init; }
}
