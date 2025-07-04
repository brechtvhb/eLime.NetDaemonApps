using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace eLime.NetDaemonApps.Logging;

public static class CustomLoggingProvider
{
    /// <summary>
    ///     Adds standard Serilog logging configuration, from appsettings, as per:
    ///     https://github.com/datalust/dotnet6-serilog-example
    /// </summary>
    /// <param name="builder"></param>
    public static IHostBuilder UseCustomLogging(this IHostBuilder builder)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.development.json", optional: true)
            .Build();

        var logger = new LoggerConfiguration()
            .Enrich.With(new ClassNameEnricher())
            .ReadFrom.Configuration(configuration)
            .CreateLogger();

        return builder.UseSerilog(logger);
    }
}

public class ClassNameEnricher : ILogEventEnricher
{

    public ClassNameEnricher()
    {

    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        logEvent.Properties.TryGetValue("SourceContext", out var sourceContext);
        var sourceContextAsString = sourceContext?.ToString();

        var property = propertyFactory.CreateProperty("ClassName", sourceContextAsString?[(sourceContextAsString.LastIndexOf('.') + 1)..]?.Replace("\"", "") ?? sourceContextAsString);
        logEvent.AddPropertyIfAbsent(property);
    }
}