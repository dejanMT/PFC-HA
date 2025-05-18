using Dejan_Camilleri_SWD63B.DataAccess;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Dejan_Camilleri_SWD63B.Controllers
{
    public class AccountController : Controller
    {
        private readonly FirestoreRepository _userRepo;

        public AccountController(FirestoreRepository userRepo)
        {
            _userRepo = userRepo;
        }

        // Redirects to Google for sign-in
        [HttpGet]
        public IActionResult Login()
        {
            var props = new AuthenticationProperties
            {
                RedirectUri = Url.Action(nameof(GoogleResponse))
            };
            return Challenge(props, GoogleDefaults.AuthenticationScheme);
        }

        // This is the callback URL that Google redirects to after sign-in
        [HttpGet]
        public async Task<IActionResult> GoogleResponse()
        {
            // simply let the Google handler finalize the sign-in
            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!result.Succeeded)
                    return RedirectToAction("Index", "Home");
            
            // at this point, result.Principal.Claims already includes ClaimTypes.Role
            return RedirectToAction("Index", "Home");
        }

        // Sign out of our cookie
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        // Switch the user's role between Technician and User
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> SwitchRole()
        {
            // Grab the Firestore user-doc ID from the existing claim
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
                return Unauthorized();

            // Load the user, flip their Role in Firestore
            var user = await _userRepo.GetUserByIdAsync(userId);
            if (user == null)
                return NotFound();
            var newRole = user.Role == "Technician" ? "User" : "Technician";
            await _userRepo.UpdateUserRoleAsync(userId, newRole);

            // Build a brand-new ClaimsPrincipal with the updated Role claim
            var updatedClaims = User.Claims
                                    .Where(c => c.Type != ClaimTypes.Role)
                                    .ToList();
            updatedClaims.Add(new Claim(ClaimTypes.Role, newRole));
            var updatedIdentity = new ClaimsIdentity(
                updatedClaims,
                CookieAuthenticationDefaults.AuthenticationScheme);
            var updatedPrincipal = new ClaimsPrincipal(updatedIdentity);

            // Overwrite the existing auth-cookie
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                updatedPrincipal);

            return RedirectToAction("Index", "Home");
        }


        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }


    }
}
