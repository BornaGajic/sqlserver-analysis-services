using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;
using Xunit.Abstractions;

namespace SqlServerAnalysisServices.Test;

public abstract class TestSetup
{
    protected readonly ITestOutputHelper _testOutputHelper;

    protected TestSetup(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    protected TestSetup()
        : this(NullTestOutputHelper.Instance)
    {
    }

    public static IConfigurationRoot SetupConfiguration<T>() => SetupConfiguration(typeof(T).Assembly);

    public static IConfigurationRoot SetupConfiguration() => SetupConfiguration(Assembly.GetExecutingAssembly());

    public static IConfigurationRoot SetupConfiguration(Assembly assembly)
    {
        return new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", true, false)
            .AddUserSecrets(assembly, true)
            .Build();
    }

    public static IServiceProvider SetupContainer(Action<IServiceCollection> callback)
    {
        var services = new ServiceCollection();
        services.TryAddSingleton<ILoggerFactory>(new NullLoggerFactory());

        callback?.Invoke(services);
        return services.BuildServiceProvider();
    }

    private class NullTestOutputHelper : ITestOutputHelper
    {
        public static NullTestOutputHelper Instance = new();

        public void WriteLine(string message)
        { }

        public void WriteLine(string format, params object[] args)
        { }
    }
}