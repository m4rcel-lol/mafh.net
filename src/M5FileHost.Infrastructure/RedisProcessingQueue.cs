using System.Text.Json;
using M5FileHost.Core;
using StackExchange.Redis;

namespace M5FileHost.Infrastructure;

public sealed class RedisProcessingQueue(IConnectionMultiplexer redis) : IProcessingQueue
{
    private const string QueueKey = "m5filehost:processing:v1";
    private readonly IDatabase _database = redis.GetDatabase();

    public async Task EnqueueAsync(ProcessingMessage message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _database.ListRightPushAsync(QueueKey, JsonSerializer.Serialize(message));
    }

    public async Task<ProcessingMessage?> DequeueAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var value = await _database.ListLeftPopAsync(QueueKey);
            if (value.HasValue) return JsonSerializer.Deserialize<ProcessingMessage>((string)value!);
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }
        return null;
    }
}
