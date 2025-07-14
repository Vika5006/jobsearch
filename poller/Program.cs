using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Azure.Identity;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

Host.CreateDefaultBuilder()
    .ConfigureFunctionsWorkerDefaults()
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
        // Remove default filter that blocks Info-level logs
        logging.Services.Configure<LoggerFilterOptions>(options =>
        {
            var defaultRule = options.Rules.FirstOrDefault(rule =>
                rule.ProviderName == "Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider");

            if (defaultRule is not null)
            {
                options.Rules.Remove(defaultRule);
            }
        });
    })
    .Build()
    .Run();
        