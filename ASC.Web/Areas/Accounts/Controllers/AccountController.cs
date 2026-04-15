using ASC.Utilities;
using ASC.Web.Areas.Accounts.Models;
using ASC.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ASC.Web.Areas.Accounts.Controllers
{
    [Authorize]
    [Area("Accounts")]
    public class AccountController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly SignInManager<IdentityUser> _signInManager;

        public AccountController(
            UserManager<IdentityUser> userManager,
            IEmailSender emailSender,
            SignInManager<IdentityUser> signInManager)
        {
            _userManager = userManager;
            _emailSender = emailSender;
            _signInManager = signInManager;
        }

        public IActionResult Index()
        {
            return View();
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> ServiceEngineers()
        {
            var serviceEngineers = await _userManager.GetUsersInRoleAsync("Engineer");

            // Hold all service engineers in session
            HttpContext.Session.SetSession("ServiceEngineers", serviceEngineers);

            return View(new ServiceEngineerViewModel
            {
                ServiceEngineers = serviceEngineers == null ? null : serviceEngineers.ToList(),
                Registration = new ServiceEngineerRegistrationViewModel { IsEdit = false }
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ServiceEngineers(ServiceEngineerViewModel serviceEngineer)
        {
            serviceEngineer.ServiceEngineers = HttpContext.Session.GetSession<List<IdentityUser>>("ServiceEngineers");

            if (!ModelState.IsValid)
            {
                return View(serviceEngineer);
            }

            if (serviceEngineer.Registration.IsEdit)
            {
                // Update User
                var user = await _userManager.FindByEmailAsync(serviceEngineer.Registration.Email);
                if (user == null)
                {
                    ModelState.AddModelError("", "User not found.");
                    return View(serviceEngineer);
                }

                user.UserName = serviceEngineer.Registration.UserName;

                IdentityResult result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    result.Errors.ToList().ForEach(p => ModelState.AddModelError("", p.Description));
                    return View(serviceEngineer);
                }

                // Update Password
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                IdentityResult passwordResult = await _userManager.ResetPasswordAsync(
                    user,
                    token,
                    serviceEngineer.Registration.Password);

                if (!passwordResult.Succeeded)
                {
                    passwordResult.Errors.ToList().ForEach(p => ModelState.AddModelError("", p.Description));
                    return View(serviceEngineer);
                }

                // Update claims
                user = await _userManager.FindByEmailAsync(serviceEngineer.Registration.Email);
                var identity = await _userManager.GetClaimsAsync(user);

                var isActiveClaim = identity.SingleOrDefault(p => p.Type == "IsActive");
                if (isActiveClaim != null)
                {
                    await _userManager.RemoveClaimAsync(user, new Claim(isActiveClaim.Type, isActiveClaim.Value));
                }

                await _userManager.AddClaimAsync(
                    user,
                    new Claim("IsActive", serviceEngineer.Registration.IsActive.ToString()));
            }
            else
            {
                // Create User
                IdentityUser user = new IdentityUser
                {
                    UserName = serviceEngineer.Registration.UserName,
                    Email = serviceEngineer.Registration.Email,
                    EmailConfirmed = true
                };

                IdentityResult result = await _userManager.CreateAsync(user, serviceEngineer.Registration.Password);

                if (!result.Succeeded)
                {
                    result.Errors.ToList().ForEach(p => ModelState.AddModelError("", p.Description));
                    return View(serviceEngineer);
                }

                await _userManager.AddClaimAsync(
                    user,
                    new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress",
                    serviceEngineer.Registration.Email));

                await _userManager.AddClaimAsync(
                    user,
                    new Claim(ClaimTypes.Email, serviceEngineer.Registration.Email));

                await _userManager.AddClaimAsync(
                    user,
                    new Claim(ClaimTypes.Name, serviceEngineer.Registration.UserName));

                await _userManager.AddClaimAsync(
                    user,
                    new Claim("IsActive", serviceEngineer.Registration.IsActive.ToString()));

                // Assign user to Engineer Role
                var roleResult = await _userManager.AddToRoleAsync(user, "Engineer");
                if (!roleResult.Succeeded)
                {
                    roleResult.Errors.ToList().ForEach(p => ModelState.AddModelError("", p.Description));
                    return View(serviceEngineer);
                }
            }

            if (serviceEngineer.Registration.IsActive)
            {
                await _emailSender.SendEmailAsync(
                    serviceEngineer.Registration.Email,
                    "Account Created/Modified",
                    $"Email : {serviceEngineer.Registration.Email} / Password : {serviceEngineer.Registration.Password}");
            }
            else
            {
                await _emailSender.SendEmailAsync(
                    serviceEngineer.Registration.Email,
                    "Account Deactivated",
                    "Your account has been deactivated.");
            }

            return RedirectToAction("ServiceEngineers");
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Customers()
        {
            var customers = await _userManager.GetUsersInRoleAsync("User");

            // Hold all customers in session
            HttpContext.Session.SetSession("Customers", customers);

            return View(new CustomerViewModel
            {
                Customers = customers == null ? null : customers.ToList(),
                Registration = new CustomerRegistrationViewModel { IsEdit = false }
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Customers(CustomerViewModel customer)
        {
            customer.Customers = HttpContext.Session.GetSession<List<IdentityUser>>("Customers");

            if (!ModelState.IsValid)
            {
                return View(customer);
            }

            if (customer.Registration.IsEdit)
            {
                // Update User
                // Update claims IsActive
                var user = await _userManager.FindByEmailAsync(customer.Registration.Email);
                if (user == null)
                {
                    ModelState.AddModelError("", "User not found.");
                    return View(customer);
                }

                var identity = await _userManager.GetClaimsAsync(user);
                var isActiveClaim = identity.SingleOrDefault(p => p.Type == "IsActive");

                if (isActiveClaim != null)
                {
                    await _userManager.RemoveClaimAsync(
                        user,
                        new Claim(isActiveClaim.Type, isActiveClaim.Value));
                }

                await _userManager.AddClaimAsync(
                    user,
                    new Claim("IsActive", customer.Registration.IsActive.ToString()));
            }

            if (customer.Registration.IsActive)
            {
                await _emailSender.SendEmailAsync(
                    customer.Registration.Email,
                    "Account Modified",
                    $"Your account has been activated, Email : {customer.Registration.Email}");
            }
            else
            {
                await _emailSender.SendEmailAsync(
                    customer.Registration.Email,
                    "Account Deactivated",
                    "Your account has been deactivated.");
            }

            return RedirectToAction("Customers");
        }

        [HttpGet]
        public IActionResult Profile()
        {
            var user = HttpContext.User.GetCurrentUserDetails();

            return View(new ProfileModel()
            {
                UserName = user.Name
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(ProfileModel profile)
        {
            if (!ModelState.IsValid)
            {
                return View(profile);
            }

            // Update UserName
            var currentUser = HttpContext.User.GetCurrentUserDetails();
            var user = await _userManager.FindByEmailAsync(currentUser.Email);

            if (user == null)
            {
                ModelState.AddModelError("", "User not found.");
                return View(profile);
            }

            user.UserName = profile.UserName;

            IdentityResult result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                result.Errors.ToList().ForEach(p => ModelState.AddModelError("", p.Description));
                return View(profile);
            }

            var claims = await _userManager.GetClaimsAsync(user);
            var nameClaim = claims.SingleOrDefault(p => p.Type == ClaimTypes.Name);

            if (nameClaim != null)
            {
                await _userManager.RemoveClaimAsync(user, new Claim(nameClaim.Type, nameClaim.Value));
            }

            await _userManager.AddClaimAsync(user, new Claim(ClaimTypes.Name, profile.UserName ?? string.Empty));

            await _signInManager.RefreshSignInAsync(user);

            return RedirectToAction("Dashboard", "Dashboard", new { area = "ServiceRequests" });
        }
    }
}