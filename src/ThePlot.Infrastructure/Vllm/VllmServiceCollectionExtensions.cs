using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace ThePlot.Infrastructure.Vllm;

public static class VllmServiceCollectionExtensions
{
    public static IServiceCollection AddVllmGpuManagement(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<VllmOptions>(configuration.GetSection(VllmOptions.SectionName));
        services.AddHttpClient<ISupervisordClient, SupervisordClient>("supervisord")
            .AddStandardResilienceHandler();
        services.AddHttpClient("vllm-health")
            .AddStandardResilienceHandler();
        services.AddSingleton<GpuModelManager>();
        return services;
    }
}
