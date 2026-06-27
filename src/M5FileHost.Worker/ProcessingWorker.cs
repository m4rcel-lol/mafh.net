using M5FileHost.Core;
using M5FileHost.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace M5FileHost.Worker;

public sealed class ProcessingWorker(IServiceScopeFactory scopes, IProcessingQueue queue, ILogger<ProcessingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverPendingAsync(stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            ProcessingMessage? message;
            using (var poll = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken))
            {
                poll.CancelAfter(TimeSpan.FromMinutes(5));
                try { message = await queue.DequeueAsync(poll.Token); }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested) { await RecoverPendingAsync(stoppingToken); continue; }
            }
            if (message is null) continue;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IFileProcessor>().ProcessAsync(message.FileId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Unhandled failure processing file {FileId}", message.FileId); }
        }
    }

    private async Task RecoverPendingAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var staleBefore = DateTimeOffset.UtcNow.AddHours(-1);
        await database.Files.Where(x => x.ProcessingStatus == ProcessingStatus.Processing && x.UpdatedAt < staleBefore)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.ProcessingStatus, ProcessingStatus.Pending).SetProperty(x => x.ProcessingError, "Recovered after an interrupted worker."), cancellationToken);
        var pending = await database.Files.AsNoTracking().Where(x => x.ProcessingStatus == ProcessingStatus.Pending).OrderBy(x => x.CreatedAt).Select(x => x.Id).Take(1_000).ToListAsync(cancellationToken);
        foreach (var id in pending) await queue.EnqueueAsync(new(id), cancellationToken);
        if (pending.Count > 0) logger.LogInformation("Recovered {Count} pending processing jobs", pending.Count);
    }
}
