using ASC.Model.Models;

namespace ASC.Web.Models
{
    public class MasterDataCache
    {
        public List<MasterDataKey> MasterDataKeys { get; set; } = new();
        public List<MasterDataValue> MasterDataValues { get; set; } = new();
    }
}