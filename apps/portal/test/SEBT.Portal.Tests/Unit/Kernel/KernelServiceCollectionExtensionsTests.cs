
using Microsoft.Extensions.DependencyInjection;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Telemetry;

namespace SEBT.Portal.Tests.Unit.Kernel;

public class KernelServiceCollectionExtensionsTests
{
    [Fact]
    public void RegisterQueryHandler_RegistersInstrumentedQueryHandler()
    {
        // Arrange
        var sc = CreateServiceCollection();

        // Act
        sc.RegisterQueryHandler<TestQuery, object, TestQueryHandler>();

        // Assert
        var sp = sc.BuildServiceProvider();
        var queryHandler = sp.GetRequiredService<IQueryHandler<TestQuery, object>>();
        Assert.IsType<InstrumentedQueryHandler<TestQuery, object>>(queryHandler);
    }

    [Fact]
    public void RegisterQueryHandler_WithFactory_RegistersInstrumentedQueryHandler()
    {
        // Arrange
        var sc = CreateServiceCollection();

        // Act
        sc.RegisterQueryHandler(sp => new TestQueryHandler());

        // Assert
        var sp = sc.BuildServiceProvider();
        var queryHandler = sp.GetRequiredService<IQueryHandler<TestQuery, object>>();
        Assert.IsType<InstrumentedQueryHandler<TestQuery, object>>(queryHandler);
    }

    [Fact]
    public void RegisterCommandHandler_RegistersInstrumentedCommandHandler()
    {
        // Arrange
        var sc = CreateServiceCollection();

        // Act
        sc.RegisterCommandHandler<TestCommand, TestCommandHandler>();

        // Assert
        var sp = sc.BuildServiceProvider();
        var commandHandler = sp.GetRequiredService<ICommandHandler<TestCommand>>();
        Assert.IsType<InstrumentedCommandHandler<TestCommand>>(commandHandler);
    }

    [Fact]
    public void RegisterCommandHandler_WithFactory_RegistersInstrumentedCommandHandler()
    {
        // Arrange
        var sc = CreateServiceCollection();

        // Act
        sc.RegisterCommandHandler(sp => new TestCommandHandler());

        // Assert
        var sp = sc.BuildServiceProvider();
        var commandHandler = sp.GetRequiredService<ICommandHandler<TestCommand>>();
        Assert.IsType<InstrumentedCommandHandler<TestCommand>>(commandHandler);
    }

    [Fact]
    public void RegisterCommandHandler_WithResult_RegistersInstrumentedCommandHandler()
    {
        // Arrange
        var sc = CreateServiceCollection();

        // Act
        sc.RegisterCommandHandler<TestCommandWithResult, object, TestCommandWithResultHandler>();

        // Assert
        var sp = sc.BuildServiceProvider();
        var commandHandler = sp.GetRequiredService<ICommandHandler<TestCommandWithResult, object>>();
        Assert.IsType<InstrumentedCommandHandler<TestCommandWithResult, object>>(commandHandler);
    }

    [Fact]
    public void RegisterCommandHandler_WithResult_WithFactory_RegistersInstrumentedCommandHandler()
    {
        // Arrange
        var sc = CreateServiceCollection();

        // Act
        sc.RegisterCommandHandler(sp => new TestCommandWithResultHandler());

        // Assert
        var sp = sc.BuildServiceProvider();
        var commandHandler = sp.GetRequiredService<ICommandHandler<TestCommandWithResult, object>>();
        Assert.IsType<InstrumentedCommandHandler<TestCommandWithResult, object>>(commandHandler);
    }

    private static IServiceCollection CreateServiceCollection() =>
        new ServiceCollection().AddSingleton<IInstrumentationSource, InstrumentationSource>();

    private class TestQuery : IQuery<object>;

    private class TestQueryHandler : IQueryHandler<TestQuery, object>
    {
        public Task<Result<object>> Handle(TestQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<object>.Success(new { }));
    }

    private class TestCommand : ICommand;

    private class TestCommandHandler : ICommandHandler<TestCommand>
    {
        public Task<Result> Handle(TestCommand command, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());
    }

    private class TestCommandWithResult : ICommand<object>;

    private class TestCommandWithResultHandler : ICommandHandler<TestCommandWithResult, object>
    {
        public Task<Result<object>> Handle(TestCommandWithResult command, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<object>.Success(new { }));
    }
}
