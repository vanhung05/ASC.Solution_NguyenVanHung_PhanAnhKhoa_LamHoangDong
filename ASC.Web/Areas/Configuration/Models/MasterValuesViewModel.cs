using System.Collections.Generic;

namespace ASC.Web.Areas.Configuration.Models
{
    public class MasterValuesViewModel
    {
        public List<MasterDataValueViewModel> MasterValues { get; set; } = new();
        public MasterDataValueViewModel MasterValueInContext { get; set; } = new MasterDataValueViewModel();
        public bool IsEdit { get; set; }
    }
}