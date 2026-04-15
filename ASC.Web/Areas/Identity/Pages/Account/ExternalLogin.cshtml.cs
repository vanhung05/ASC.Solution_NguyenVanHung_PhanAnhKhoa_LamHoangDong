using System;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading.Tasks;
using ASC.Web.Configuration;
using ASC.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ASC.Web.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ExternalLoginModel : PageModel
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IUserStore<IdentityUser> _userStore;
        private readonly IUserEmailStore<IdentityUser> _emailStore;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<ExternalLoginModel> _logger;
        private readonly IOptions<ApplicationSettings> _applicationSettings;

        public ExternalLoginModel(
            SignInManager<IdentityUser> signInManager,
            UserManager<IdentityUser> userManager,
            IUserStore<IdentityUser> userStore,
            ILogger<ExternalLoginModel> logger,
            IEmailSender emailSender,
            IOptions<ApplicationSettings> applicationSettings)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _logger = logger;
            _emailSender = emailSender;
            _applicationSettings = applicationSettings;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ProviderDisplayName { get; set; }

        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }
        }

        public IActionResult OnGet() => RedirectToPage("./Login");

        public IActionResult OnPost(string provider, string returnUrl = null)
        {
            var redirectUrl = Url.Page("./ExternalLogin", pageHandler: "Callback", values: new { returnUrl });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return new ChallengeResult(provider, properties);
        }

        public async Task<IActionResult> OnGetCallbackAsync(string returnUrl = null, string remoteError = null)
        {
            returnUrl ??= Url.Content("~/");

            if (remoteError != null)
            {
                ErrorMessage = $"Error from external provider: {remoteError}";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                ErrorMessage = "Error loading external login information.";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            var result = await _signInManager.ExternalLoginSignInAsync(
                info.LoginProvider,
                info.ProviderKey,
                isPersistent: false,
                bypassTwoFactor: true);

            if (result.Succeeded)
            {
                var existingEmail = info.Principal.FindFirstValue(ClaimTypes.Email);
                if (!string.IsNullOrWhiteSpace(existingEmail))
                {
                    var existingUser = await _userManager.FindByEmailAsync(existingEmail);
                    if (existingUser != null)
                    {
                        await EnsureRequiredClaimsAsync(existingUser);
                    }
                }

                _logger.LogInformation("{Name} logged in with {LoginProvider} provider.",
                    info.Principal.Identity?.Name, info.LoginProvider);

                return RedirectToAction("Dashboard", "Dashboard", new { area = "ServiceRequests" });
            }

            if (result.IsLockedOut)
            {
                return RedirectToPage("./Lockout");
            }

            ReturnUrl = returnUrl;
            ProviderDisplayName = info.ProviderDisplayName;

            if (info.Principal.HasClaim(c => c.Type == ClaimTypes.Email))
            {
                var email = info.Principal.FindFirstValue(ClaimTypes.Email);
                var appSettings = _applicationSettings.Value;

                Input = new InputModel
                {
                    Email = email
                };

                if (!string.IsNullOrWhiteSpace(email) &&
                    (email.Equals(appSettings.AdminEmail, StringComparison.OrdinalIgnoreCase) ||
                     email.Equals(appSettings.EngineerEmail, StringComparison.OrdinalIgnoreCase)))
                {
                    ModelState.AddModelError(string.Empty, $"Email '{email}' is already taken.");
                    return Page();
                }
            }

            return Page();
        }

        public async Task<IActionResult> OnPostConfirmationAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            ProviderDisplayName = info.ProviderDisplayName;
            ReturnUrl = returnUrl;

            var appSettings = _applicationSettings.Value;

            if (Input.Email.Equals(appSettings.AdminEmail, StringComparison.OrdinalIgnoreCase) ||
                Input.Email.Equals(appSettings.EngineerEmail, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(string.Empty, $"Email '{Input.Email}' is already taken.");
                return Page();
            }

            var existedUser = await _userManager.FindByEmailAsync(Input.Email);
            if (existedUser != null)
            {
                var existedRoles = await _userManager.GetRolesAsync(existedUser);

                if (existedRoles.Contains("Admin") || existedRoles.Contains("Engineer"))
                {
                    ModelState.AddModelError(string.Empty, $"Email '{Input.Email}' is already taken.");
                    return Page();
                }

                var existedLogins = await _userManager.GetLoginsAsync(existedUser);
                var hasCurrentLogin = false;

                foreach (var login in existedLogins)
                {
                    if (login.LoginProvider == info.LoginProvider && login.ProviderKey == info.ProviderKey)
                    {
                        hasCurrentLogin = true;
                        break;
                    }
                }

                if (!hasCurrentLogin)
                {
                    var existedAddLoginResult = await _userManager.AddLoginAsync(existedUser, info);
                    if (!existedAddLoginResult.Succeeded)
                    {
                        foreach (var error in existedAddLoginResult.Errors)
                        {
                            ModelState.AddModelError("", error.Description);
                        }

                        return Page();
                    }
                }

                await EnsureRequiredClaimsAsync(existedUser);
                await _signInManager.SignInAsync(existedUser, isPersistent: false);

                return RedirectToAction("Dashboard", "Dashboard", new { area = "ServiceRequests" });
            }

            var user = new IdentityUser
            {
                UserName = Input.Email,
                Email = Input.Email,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }

                return Page();
            }

            await EnsureRequiredClaimsAsync(user);

            var roleResult = await _userManager.AddToRoleAsync(user, "User");
            if (!roleResult.Succeeded)
            {
                foreach (var error in roleResult.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }

                return Page();
            }

            result = await _userManager.AddLoginAsync(user, info);
            if (result.Succeeded)
            {
                await _signInManager.SignInAsync(user, isPersistent: false);
                _logger.LogInformation("User created an account using {Name} provider.", info.LoginProvider);

                return RedirectToAction("Dashboard", "Dashboard", new { area = "ServiceRequests" });
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
        }

        private async Task EnsureRequiredClaimsAsync(IdentityUser user)
        {
            var claims = await _userManager.GetClaimsAsync(user);

            if (!claims.Any(c => c.Type == ClaimTypes.Name))
            {
                await _userManager.AddClaimAsync(user, new Claim(ClaimTypes.Name, user.UserName ?? user.Email ?? "User"));
            }

            if (!claims.Any(c => c.Type == ClaimTypes.Email) && !string.IsNullOrWhiteSpace(user.Email))
            {
                await _userManager.AddClaimAsync(user, new Claim(ClaimTypes.Email, user.Email));
            }

            if (!claims.Any(c => c.Type == "IsActive"))
            {
                await _userManager.AddClaimAsync(user, new Claim("IsActive", "True"));
            }
        }

        private IUserEmailStore<IdentityUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException("The default UI requires a user store with email support.");
            }

            return (IUserEmailStore<IdentityUser>)_userStore;
        }
    }
}