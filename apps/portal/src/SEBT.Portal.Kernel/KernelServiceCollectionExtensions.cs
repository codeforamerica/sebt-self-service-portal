using Microsoft.Extensions.DependencyInjection;
using SEBT.Portal.Kernel.Telemetry;

namespace SEBT.Portal.Kernel;

public static class KernelServiceCollectionExtensions
{
    public static IServiceCollection RegisterQueryHandler<TQuery, TResult, TQueryHandler>(this IServiceCollection services)
        where TQuery : IQuery<TResult>
        where TQueryHandler : class, IQueryHandler<TQuery, TResult>
    {
        services.AddTransient<IValidator<TQuery>, DataAnnotationsValidator<TQuery>>();
        services.AddTransient<IQueryHandler<TQuery, TResult>>(sp =>
        {
            var inner = ActivatorUtilities.CreateInstance<TQueryHandler>(sp);
            var instrumentationSource = sp.GetService<IInstrumentationSource>();
            return instrumentationSource is not null
                ? new InstrumentedQueryHandler<TQuery, TResult>(inner, instrumentationSource)
                : inner;
        });

        return services;
    }

    public static IServiceCollection RegisterQueryHandler<TQuery, TResult>(
        this IServiceCollection services,
        Func<IServiceProvider, IQueryHandler<TQuery, TResult>> queryHandlerFactory)
        where TQuery : IQuery<TResult>
    {
        services.AddTransient<IValidator<TQuery>, DataAnnotationsValidator<TQuery>>();
        services.AddTransient<IQueryHandler<TQuery, TResult>>(sp =>
        {
            var inner = queryHandlerFactory(sp);
            var instrumentationSource = sp.GetService<IInstrumentationSource>();
            return instrumentationSource is not null
                ? new InstrumentedQueryHandler<TQuery, TResult>(inner, instrumentationSource)
                : inner;
        });

        return services;
    }

    public static IServiceCollection RegisterCommandHandler<TCommand, TResult, TCommandHandler>(this IServiceCollection services)
        where TCommand : ICommand<TResult>
        where TCommandHandler : class, ICommandHandler<TCommand, TResult>
    {
        services.AddTransient<IValidator<TCommand>, DataAnnotationsValidator<TCommand>>();
        services.AddTransient<ICommandHandler<TCommand, TResult>>(sp =>
        {
            var inner = ActivatorUtilities.CreateInstance<TCommandHandler>(sp);
            var instrumentationSource = sp.GetService<IInstrumentationSource>();
            return instrumentationSource is not null
                ? new InstrumentedCommandHandler<TCommand, TResult>(inner, instrumentationSource)
                : inner;
        });

        return services;
    }

    public static IServiceCollection RegisterCommandHandler<TCommand, TResult>(
        this IServiceCollection services,
        Func<IServiceProvider, ICommandHandler<TCommand, TResult>> commandHandlerFactory)
        where TCommand : ICommand<TResult>
    {
        services.AddTransient<IValidator<TCommand>, DataAnnotationsValidator<TCommand>>();
        services.AddTransient<ICommandHandler<TCommand, TResult>>(sp =>
        {
            var inner = commandHandlerFactory(sp);
            var instrumentationSource = sp.GetService<IInstrumentationSource>();
            return instrumentationSource is not null
                ? new InstrumentedCommandHandler<TCommand, TResult>(inner, instrumentationSource)
                : inner;
        });

        return services;
    }

    public static IServiceCollection RegisterCommandHandler<TCommand, TCommandHandler>(this IServiceCollection services)
        where TCommand : ICommand
        where TCommandHandler : class, ICommandHandler<TCommand>
    {
        services.AddTransient<IValidator<TCommand>, DataAnnotationsValidator<TCommand>>();
        services.AddTransient<ICommandHandler<TCommand>>(sp =>
        {
            var inner = ActivatorUtilities.CreateInstance<TCommandHandler>(sp);
            var instrumentationSource = sp.GetService<IInstrumentationSource>();
            return instrumentationSource is not null
                ? new InstrumentedCommandHandler<TCommand>(inner, instrumentationSource)
                : inner;
        });

        return services;
    }

    public static IServiceCollection RegisterCommandHandler<TCommand>(
        this IServiceCollection services,
        Func<IServiceProvider, ICommandHandler<TCommand>> commandHandlerFactory)
        where TCommand : ICommand
    {
        services.AddTransient<IValidator<TCommand>, DataAnnotationsValidator<TCommand>>();
        services.AddTransient<ICommandHandler<TCommand>>(sp =>
        {
            var inner = commandHandlerFactory(sp);
            var instrumentationSource = sp.GetService<IInstrumentationSource>();
            return instrumentationSource is not null
                ? new InstrumentedCommandHandler<TCommand>(inner, instrumentationSource)
                : inner;
        });

        return services;
    }

    public static IServiceCollection RegisterDataAnnotationsValidation(this IServiceCollection services)
    {
        services.AddTransient(typeof(IValidator<>), typeof(DataAnnotationsValidator<>));

        return services;
    }
}
