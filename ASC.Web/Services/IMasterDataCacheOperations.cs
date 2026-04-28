using ASC.Web.Models;

namespace ASC.Web.Services
{
    public interface IMasterDataCacheOperations
    {
        Task CreateMasterDataCacheAsync();
        Task<MasterDataCache?> GetMasterDataCacheAsync();
    }
}