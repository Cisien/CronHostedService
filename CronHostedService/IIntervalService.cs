using System;
using System.Threading;
using System.Threading.Tasks;

namespace CronHostedService
{
    public interface IIntervalService
    {
        Task Execute(DateTimeOffset ticked, double intervalSeconds, CancellationToken cancellationToken);
    }
}
