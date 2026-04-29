namespace SEBT.Portal.Kernel.Telemetry;

internal class InstrumentedCommandHandler<TCommand>(
    ICommandHandler<TCommand> inner,
    IInstrumentationSource instrumentationSource
) : ICommandHandler<TCommand>
    where TCommand : ICommand
{
    public async Task<Result> Handle(TCommand command, CancellationToken cancellationToken = default)
    {
        var innerHandlerTypeName = inner.GetType().Name;
        var activityName = $"{innerHandlerTypeName}.{nameof(Handle)}()";

        using var activity = instrumentationSource.ActivitySource.StartActivity(activityName);
        return await inner.Handle(command, cancellationToken);
    }
}

internal class InstrumentedCommandHandler<TCommand, TResult>(
    ICommandHandler<TCommand, TResult> inner,
    IInstrumentationSource instrumentationSource
) : ICommandHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    public async Task<Result<TResult>> Handle(TCommand command, CancellationToken cancellationToken = default)
    {
        var innerHandlerTypeName = inner.GetType().Name;
        var activityName = $"{innerHandlerTypeName}.{nameof(Handle)}()";

        using var activity = instrumentationSource.ActivitySource.StartActivity(activityName);
        return await inner.Handle(command, cancellationToken);
    }
}

