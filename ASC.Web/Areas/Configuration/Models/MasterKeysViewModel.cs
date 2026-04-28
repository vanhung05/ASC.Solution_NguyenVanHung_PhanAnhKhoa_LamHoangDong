using System.Collections.Generic;

namespace ASC.Web.Areas.Configuration.Models
{
    public class MasterKeysViewModel
    {
        public List<MasterDataKeyViewModel>? MasterKeys { get; set; }
        public MasterDataKeyViewModel MasterKeyInContext { get; set; } = new MasterDataKeyViewModel();
        public bool IsEdit { get; set; }
    }
}