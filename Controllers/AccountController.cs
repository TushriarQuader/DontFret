using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using DontFret.Data;
using DontFret.Models;
using DontFret.Services;

namespace DontFret.Controllers;

[Authorize]
public class AccountController(UserManager<User> userManager, SignInManager<User> signInManager, ApplicationDbContext db) : Controller
{
    /// <summary>
    /// Displays the user's profile information.
    /// </summary>
    /// <returns>Returns the profile view.</returns>
    public IActionResult Profile()
    {
        var userId = User.FindFirstValue( ClaimTypes.NameIdentifier );
        var customer = db.Users
            .OfType<Customer>()
            .FirstOrDefault(c => c.Id == userId);

        if( customer is null )
        {
            var user = userManager.FindByIdAsync( userId ).Result;
            return View(user);
        }

        return View(customer);
    }

    /// <summary>
    /// Updates the user's profile information.
    /// </summary>
    /// <param name="email">The user's email address.</param>
    /// <param name="address">The user's address.</param>
    /// <param name="postcode">The user's postcode.</param>
    /// <returns>Returns the updated profile view.</returns>
    [HttpPost]
    public IActionResult Profile(string email, string address, string postcode)
    {
        var userId = User.FindFirstValue( ClaimTypes.NameIdentifier );
        var user = userManager.FindByIdAsync( userId ).Result;

        if( user is null )
            return NotFound();
        // Update email if changed
        if( user.Email != email )
        {
            user.Email = email;
            user.UserName = email;
            userManager.UpdateAsync(user).Wait();
        }
        // Update address and postcode for all user types
        user.Address = address;
        user.Postcode = postcode;
        db.Update( user );
        db.SaveChanges();
        // this is a bit hacky but it works - we want to refresh the user's claims to reflect any changes
        TempData["SuccessMessage"] = "Profile updated successfully.";
        return RedirectToAction( nameof( Profile ) );
    }
    /// <summary>
    /// Displays the email verification page where users can enter their verification code.
    /// This is accessed via a link in the verification email sent to users when they register.
    /// The page also allows users to request a new verification code if needed.
    /// </summary>
    /// <param name="email"></param>
    /// <returns></returns>
    [AllowAnonymous]
    public IActionResult VerifyEmail(string? email)
    {
        ViewData["Email"] = email;
        return View();
    }
    /// <summary>
    /// Handles the submission of the email verification form.
    /// It checks the provided code against the database, confirms the user's email if valid, and signs them in.
    /// If the code is invalid or expired, it shows appropriate error messages and allows the user to request a new code.
    /// </summary>
    /// <param name="email"></param>
    /// <param name="code"></param>
    /// <returns></returns>
    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> VerifyEmail(string email, string code)
    {
        if( string.IsNullOrWhiteSpace( email ) || string.IsNullOrWhiteSpace( code ) )
        {
            ModelState.AddModelError("", "Email and code are required.");
            ViewData["Email"] = email;
            return View();
        }

        var user = await userManager.FindByEmailAsync(email);
        if( user is null )
        {
            ModelState.AddModelError("", "Invalid verification request.");
            ViewData["Email"] = email;
            return View();
        }

        if( await userManager.IsEmailConfirmedAsync( user ) )
        {
            TempData["SuccessMessage"] = "Email already confirmed. You can log in.";
            return RedirectToPage("/Identity/Account/Login");
        }

        var verificationCode = await db.EmailVerificationCodes
            .Where(vc => vc.UserId == user.Id && !vc.IsUsed && vc.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(vc => vc.Id)
            .FirstOrDefaultAsync();

        if( verificationCode is null )
        {
            ModelState.AddModelError( "", "No valid verification code found. Request a new one." );
            ViewData["Email"] = email;
            return View();
        }
        // Lockout after 5 failed attempts to prevent brute force
        if( verificationCode.Attempts >= 5 )
        {
            verificationCode.IsUsed = true;
            await db.SaveChangesAsync();
            ModelState.AddModelError( "", "Too many attempts. Request a new code." );
            ViewData["Email"] = email;
            return View();
        }
        // Check if code matches
        if( verificationCode.Code != code )
        {
            verificationCode.Attempts++;
            await db.SaveChangesAsync();
            ModelState.AddModelError( "", "Invalid code. Try again." );
            ViewData["Email"] = email;
            return View();
        }
        // Code is valid, confirm email
        var result = await userManager.ConfirmEmailAsync( user, verificationCode.Token );
        if( !result.Succeeded )
        {
            ModelState.AddModelError( "", "Could not verify your email. Request a new code." );
            ViewData["Email"] = email;
            return View();
        }

        verificationCode.IsUsed = true;
        await db.SaveChangesAsync();

        await signInManager.SignInAsync( user, isPersistent: false );
        TempData["SuccessMessage"] = "Email confirmed successfully! Welcome to DontFret.";
        return RedirectToAction("Index", "Home");
    }
    /// <summary>
    /// Resends a new email verification code to the user.
    /// This is useful if the user did not receive the original email or if the code has expired. 
    /// It generates a new code, saves it to the database, and sends an email with the new code and a link to the verification page. 
    /// Old codes are marked as used to prevent multiple valid codes existing at the same time, which could be a security risk.
    /// </summary>
    /// <param name="email"></param>
    /// <returns></returns>
    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> ResendCode(string email)
    {
        if( string.IsNullOrWhiteSpace( email ) )
        {
            TempData["ErrorMessage"] = "Email is required.";
            return RedirectToAction( nameof( VerifyEmail ) );
        }

        var user = await userManager.FindByEmailAsync( email );
        if( user is null )
        {
            TempData["ErrorMessage"] = "User not found.";
            return RedirectToAction( nameof( VerifyEmail ) );
        }

        if( await userManager.IsEmailConfirmedAsync( user ) )
        {
            TempData["SuccessMessage"] = "Email already confirmed.";
            return RedirectToPage( "/Identity/Account/Login" );
        }

        var newToken = await userManager.GenerateEmailConfirmationTokenAsync( user );
        var newOtp = Random.Shared.Next( 100000, 999999 ).ToString();
        // Old codes need to be marked as used to prevent multiple valid codes existing at the same time, which could be a security risk
        var oldCodes = await db.EmailVerificationCodes
            .Where( vc => vc.UserId == user.Id && !vc.IsUsed )
            .ToListAsync();
        foreach( var old in oldCodes )
            old.IsUsed = true;

        db.EmailVerificationCodes.Add( new EmailVerificationCode
        {
            UserId = user.Id,
            Code = newOtp,
            Token = newToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes( 10 ),
            IsUsed = false,
            Attempts = 0
        });

        await db.SaveChangesAsync();
        // Tell the user to check their email for the new code - we can include a link to the verification page with the email pre-filled for convenience
        var request = HttpContext.Request;
        var baseUrl = $"{request.Scheme}://{request.Host}";
        var verifyUrl = $"{baseUrl}/Account/VerifyEmail?email={ Uri.EscapeDataString( email ) }";
        var emailService = HttpContext.RequestServices.GetRequiredService<IEmailService>();
        await emailService.SendEmailAsync( email, "Confirm your email",
            $"<p>Your new verification code: <strong>{newOtp}</strong></p>" +
            $"<p>This code expires in 10 minutes.</p>" +
            $"<p><a href='{verifyUrl}'>Enter your code here</a></p>");

        TempData["SuccessMessage"] = "A new verification code has been sent.";
        return RedirectToAction( nameof( VerifyEmail ), new { email } );
    }
}
