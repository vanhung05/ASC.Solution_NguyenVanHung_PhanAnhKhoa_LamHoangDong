using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;

namespace ASC.Web.Areas.Accounts.Models
{
    public class CustomerViewModel
    {
        public List<IdentityUser>? Customers { get; set; }

        public CustomerRegistrationViewModel Registration { get; set; }
    }
}