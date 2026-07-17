namespace SEBT.Portal.Kernel.Telemetry;

internal class InstrumentedQueryHandler<TQuery, TResult>(
    IQueryHandler<TQuery, TResult> inner,
    IInstrumentationSource instrumentationSource
) : IQueryHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    public async Task<Result<TResult>> Handle(TQuery query, CancellationToken cancellationToken = default)
    {
        var innerHandlerTypeName = inner.GetType().Name;
        var activityName = $"{innerHandlerTypeName}.{nameof(Handle)}()";

        using var activity = instrumentationSource.ActivitySource.StartActivity(activityName);
        return await inner.Handle(query, cancellationToken);
    }
}
