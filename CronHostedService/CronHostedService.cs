using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NCrontab;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CronHostedService
{
    public class CronHostedService<TIntervalExecutedService> : IHostedService where TIntervalExecutedService : IIntervalService
    {
        private readonly Timer _schedulerTimer;
        private readonly string _pattern;
        private readonly ILogger _logger;
        private readonly IServiceProvider _provider;
        private readonly CancellationTokenSource _tokenSource;
        private bool _isRunning;

        public CronHostedService(IServiceProvider provider, ILogger<CronHostedService<TIntervalExecutedService>> logger, string cron)
        {
            _schedulerTimer = new Timer(async (_) => await ExecuteIfTime(), null, Timeout.Infinite, Timeout.Infinite);
            _pattern = cron;
            _logger = logger;
            _provider = provider;
            _tokenSource = new CancellationTokenSource();
        }

        private async Task ExecuteIfTime()
        {
            var caller = typeof(TIntervalExecutedService).Name;
            var startTime = DateTimeOffset.UtcNow;
            var start = new DateTimeOffset(startTime.Year, startTime.Month, startTime.Day, startTime.Hour, startTime.Minute, startTime.Second, 0, startTime.Offset);

            _logger.LogTrace($"Checking if {caller} should run: {start:o}");
            if (_isRunning)
            {
                _logger.LogTrace($"{caller} is still running. The last run began at {start}");
                return;
            }

            var schedule = CrontabSchedule.TryParse(_pattern, new CrontabSchedule.ParseOptions { IncludingSeconds = true });

            var allOccurances = schedule.GetNextOccurrences(start.AddDays(-30).UtcDateTime, start.UtcDateTime);
            var firstOccurance = allOccurances.First();
            var secondOccurance = allOccurances.Skip(1).First();

            var diff = (secondOccurance - firstOccurance).TotalSeconds;

            var next = schedule.GetNextOccurrence(start.UtcDateTime.AddSeconds(-1));
            _logger.LogTrace($"Next execution time is: {next:o}");


            if (start.Subtract(next).TotalSeconds != 0)
            {
                _logger.LogTrace($"Not time to run, diff of {start.Subtract(next).TotalSeconds } seconds {next}");
                return;
            }

            _logger.LogInformation($"Executing {caller}: {start:o}");
            _isRunning = true;
            try
            {
                using (var scope = _provider.CreateScope())
                {
                    var instance = ActivatorUtilities.GetServiceOrCreateInstance<TIntervalExecutedService>(scope.ServiceProvider);
                    await instance.Execute(start, diff, _tokenSource.Token);
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Executing a scheduled interval failed");
            }
            finally
            {
                _isRunning = false;
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _schedulerTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(1));
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _tokenSource.Cancel();
            _tokenSource.Dispose();
            _schedulerTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _schedulerTimer.Dispose();
            return Task.CompletedTask;
        }
    }
}