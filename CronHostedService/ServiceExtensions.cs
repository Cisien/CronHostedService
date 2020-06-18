using CronHostedService;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddTimedHostedService<TIntervalExecutedService>(this IServiceCollection services, string crontabPattern) where TIntervalExecutedService : class, IIntervalService
        {
            services.AddHostedService(provider =>
                ActivatorUtilities.CreateInstance<CronHostedService<TIntervalExecutedService>>(provider, crontabPattern));

            return services;
        }
    }
}
