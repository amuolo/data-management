using Microsoft.Extensions.Hosting;

namespace Agency;

public class Manager : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // TODO: finish
            await Task.Delay(1000, stoppingToken); 
        }
    }
}
