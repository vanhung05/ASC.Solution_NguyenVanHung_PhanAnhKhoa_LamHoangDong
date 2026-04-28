using ASC.Business.Interfaces;
using ASC.Web.Models;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace ASC.Web.Services
{
    public class MasterDataCacheOperations : IMasterDataCacheOperations
    {
        private const string MasterDataCacheKey = "MasterDataCache";

        private readonly IDistributedCache _cache;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public MasterDataCacheOperations(
            IDistributedCache cache,
            IServiceScopeFactory serviceScopeFactory)
        {
            _cache = cache;
            _serviceScopeFactory = serviceScopeFactory;
        }

        public async Task CreateMasterDataCacheAsync()
        {
            using var scope = _serviceScopeFactory.CreateScope();

            var masterDataOperations = scope.ServiceProvider.GetRequiredService<IMasterDataOperations>();

            var masterDataCache = new MasterDataCache
            {
                MasterDataKeys = await masterDataOperations.GetAllMasterKeysAsync(),
                MasterDataValues = await masterDataOperations.GetAllMasterValuesAsync()
            };

            var jsonData = JsonSerializer.Serialize(masterDataCache);

            await _cache.SetStringAsync(
                MasterDataCacheKey,
                jsonData,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12)
                });
        }

        public async Task<MasterDataCache?> GetMasterDataCacheAsync()
        {
            var jsonData = await _cache.GetStringAsync(MasterDataCacheKey);

            if (string.IsNullOrWhiteSpace(jsonData))
            {
                return null;
            }

            return JsonSerializer.Deserialize<MasterDataCache>(jsonData);
        }
    }
}