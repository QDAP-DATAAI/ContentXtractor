using System.Reflection;
using ContentXtractor;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Configurations;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;

[assembly: AssemblyVersion("1.0.*")]
var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        services.AddSingleton<IOpenApiConfigurationOptions>(_ => new DefaultOpenApiConfigurationOptions()
        {
            Info = new OpenApiInfo()
            {
                Version = Assembly.GetExecutingAssembly().GetName().Version!.ToString(),
                Title = "OpenAPI Document on Content Xtractor",
            },
            OpenApiVersion = OpenApiVersionType.V3,
            DocumentFilters =
            [
                new EnumDocumentFilter(),
            ],
        });
    })
    .Build();

host.Run();