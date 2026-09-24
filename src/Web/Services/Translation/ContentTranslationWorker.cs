using Application.UseCases.TranslatorServices;
using Microsoft.Extensions.Options;

namespace Web.Services.Translation;

// Hosts ContentTranslationJobProcessor: one DI scope per job, polling while idle. Does nothing
// unless ContentTranslation:WorkerEnabled is true (off until the DBA tables are deployed).
public sealed class ContentTranslationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ContentTranslationOptions _options;
    private readonly ILogger<ContentTranslationWorker> _logger;
    private readonly string _workerId = $"{Environment.MachineName}/{Guid.NewGuid():N}"[..^16];

    public ContentTranslationWorker(IServiceScopeFactory scopeFactory, IOptions<ContentTranslationOptions> options, ILogger<ContentTranslationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.WorkerEnabled)
        {
            _logger.LogInformation("Content translation worker is disabled");
            return;
        }

        var idleDelay = TimeSpan.FromSeconds(_options.PollIntervalSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            var claimed = false;
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                claimed = await scope.ServiceProvider.GetRequiredService<ContentTranslationJobProcessor>().RunOnce(_workerId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Exception type only: provider/database messages can echo payload text.
                _logger.LogError("Content translation job iteration failed with {ExceptionType}", ex.GetType().Name);
            }

            if (!claimed)
            {
                try
                {
                    await Task.Delay(idleDelay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }
}
