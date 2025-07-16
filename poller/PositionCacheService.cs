using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;


namespace JobSearch
{
    public class PositionCacheEntity : ITableEntity
    {
        public required string PartitionKey { get; set; } // Company ID
        public required string RowKey { get; set; }       // Position ID
        public DateTime SentAt { get; set; }     // When it was sent
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }

    public class CacheServiceConfiguration
    {
        public required string StorageAccountName { get; set; }
        public required string StorageAccountKey { get; set; }
        public required Uri TableUri { get; set; }        
    }

    public class PositionCacheService
    {
        private readonly TableClient _tableClient;
        private readonly CacheServiceConfiguration _config;

        public PositionCacheService(IConfiguration config)
        {
            _config = InitializeConfiguration(config);
            _tableClient = new TableClient(_config.TableUri, "PositionCache", new TableSharedKeyCredential(_config.StorageAccountName, _config.StorageAccountKey));
            _tableClient.CreateIfNotExists();
        }

        private CacheServiceConfiguration InitializeConfiguration(IConfiguration config)
        {
            #pragma warning disable CS8601 // Possible null reference assignment.
            #pragma warning disable CS8604 // Possible null reference argument.
            var cacheConfig = new CacheServiceConfiguration
            {
                StorageAccountName = config["StorageAccountName"],
                StorageAccountKey = config["StorageAccountKey"],
                TableUri = config["TableUri"] != null ? new Uri(config["TableUri"]) : new Uri($"https://{config["StorageAccountName"]}.table.core.windows.net/")
            };
            #pragma warning restore CS8604 // Possible null reference argument.
            #pragma warning restore CS8601 // Possible null reference assignment.

            return cacheConfig;
        }

        public async Task<bool> WasNewPositionSentAsync(string companyId, string positionId)
        {
            // could be separate timer function, but this is simpler for npw
            await PurgeOldEntriesAsync();

            var entity = await _tableClient.GetEntityIfExistsAsync<PositionCacheEntity>(companyId, positionId);

            if (entity.HasValue)
                return false;

            await _tableClient.AddEntityAsync(new PositionCacheEntity
            {
                PartitionKey = companyId,
                RowKey = positionId,
                SentAt = DateTime.UtcNow
            });

            return true;
        }

        public async Task PurgeOldEntriesAsync()
        {
            var query = _tableClient.QueryAsync<PositionCacheEntity>(
                filter: $"SentAt lt datetime'{DateTime.UtcNow.AddDays(-2):o}'"
            );

            await foreach (var entity in query)
                await _tableClient.DeleteEntityAsync(entity.PartitionKey, entity.RowKey);
        }
    }
}