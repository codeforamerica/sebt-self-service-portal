using Microsoft.Extensions.DependencyInjection;

namespace SEBT.Portal.Kernel;

public static class KernelServiceCollectionExtensions
{
    public static IServiceCollection RegisterQueryHandler<TQuery, TResult, TQueryHandler>(this IServiceCollection services)
        where TQuery : IQuery<TResult>
        where TQueryHandler : class, IQueryHandler<TQuery, TResult>
    {
        services.AddTransient<IValidator<TQuery>, DataAnnotationsValidator<TQuery>>();
        services.AddTransient<IQueryHandler<TQuery, TResult>, TQueryHandler>();

        return services;
    }
    
    public static IServiceCollection RegisterQueryHandler<TQuery, TResult>(
        this IServiceCollection services,
        Func<IServiceProvider, IQueryHandler<TQuery, TResult>> queryHandlerFactory)
        where TQuery : IQuery<TResult>
    {
        services.AddTransient<IValidator<TQuery>, DataAnnotationsValidator<TQuery>>();
        services.AddTransient(queryHandlerFactory);

        return services;
    }

    public static IServiceCollection RegisterCommandHandler<TCommand, TResult, TCommandHandler>(this IServiceCollection services)
        where TCommand : ICommand<TResult>
        where TCommandHandler : class, ICommandHandler<TCommand, TResult>
    {
        services.AddTransient<IValidator<TCommand>, DataAnnotationsValidator<TCommand>>();
        services.AddTransient<ICommandHandler<TCommand, TResult>, TCommandHandler>();
        
        return services;
    }
    
    public static IServiceCollection RegisterCommandHandler<TCommand, TResult>(
        this IServiceCollection services,
        Func<IServiceProvider, ICommandHandler<TCommand, TResult>> commandHandlerFactory)
        where TCommand : ICommand<TResult>
    {
        services.AddTransient<IValidator<TCommand>, DataAnnotationsValidator<TCommand>>();
        services.AddTransient(commandHandlerFactory);
        
        return services;
    }
    
    public static IServiceCollection RegisterCommandHandler<TCommand, TCommandHandler>(this IServiceCollection services)
        where TCommand : ICommand
        where TCommandHandler : class, ICommandHandler<TCommand>
    {
        services.AddTransient<IValidator<TCommand>, DataAnnotationsValidator<TCommand>>();
        services.AddTransient<ICommandHandler<TCommand>, TCommandHandler>();
        
        return services;
    }
    
    public static IServiceCollection RegisterCommandHandler<TCommand>(
        this IServiceCollection services,
        Func<IServiceProvider, ICommandHandler<TCommand>> commandHandlerFactory)
        where TCommand : ICommand
    {
        services.AddTransient<IValidator<TCommand>, DataAnnotationsValidator<TCommand>>();
        services.AddTransient(commandHandlerFactory);
        
        return services;
    }

    public static IServiceCollection RegisterDataAnnotationsValidation(this IServiceCollection services)
    {
        services.AddTransient(typeof(IValidator<>), typeof(DataAnnotationsValidator<>));
        
        return services;
    } 
}
