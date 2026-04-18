using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ASC.Web.Areas.Configuration.Models
{
    public class MasterKeysViewModel
    {
        [ValidateNever]
        public List<MasterDataKeyViewModel>? MasterKeys { get; set; }
        public MasterDataKeyViewModel? MasterKeyInContext { get; set; }
        public bool IsEdit { get; set; }
    }
}