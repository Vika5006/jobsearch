using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Azure.Identity;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.ApplicationInsights;
using Microsoft.AspNetCore.Hosting;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;

var host = Host.CreateDefaultBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureAppConfiguration((context, config) =>
    {
        config.SetBasePath(Environment.CurrentDirectory);
        config.AddEnvironmentVariables();

        var tempConfig = config.Build();
        var keyVaultEndpoint = tempConfig["AzureKeyVaultEndpoint"];
        if (!string.IsNullOrEmpty(keyVaultEndpoint))
        {
            config.AddAzureKeyVault(new Uri(keyVaultEndpoint), new DefaultAzureCredential(), new KeyVaultSecretManager());
        }
    })
     .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
    })
    .ConfigureLogging(logging =>
    {
        logging.Services.Configure<LoggerFilterOptions>(options =>
        {
            options.MinLevel = LogLevel.Trace; // Allow all levels

            var defaultRule = options.Rules.FirstOrDefault(rule =>
                rule.ProviderName == "Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider");

            if (defaultRule is not null)
            {
                options.Rules.Remove(defaultRule);
            }

            options.AddFilter<ApplicationInsightsLoggerProvider>("", LogLevel.Information);
        });

    })
    .Build();

AppDomain.CurrentDomain.ProcessExit += (s, e) =>
{    
    var connectionString = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
    if (!string.IsNullOrEmpty(connectionString))
    {
        var telemetryConfig = new TelemetryConfiguration();
        telemetryConfig.ConnectionString = connectionString;
        var telemetryClient = new TelemetryClient(telemetryConfig);
        telemetryClient.TrackTrace("Flushing telemetry on shutdown.");
        telemetryClient.Flush();
        Thread.Sleep(1000); // Give time to flush
    }
};

host.Run();

    