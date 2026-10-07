using System.Security.Claims;
using Kt19Chat.Data;
using Kt19Chat.Models;
using Kt19Chat.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kt19Chat.Controllers;

public class AccountController(AppDbContext database, IPasswordHasher<AppUser> passwordHasher, ChatSessionStore sessions) : Controller
{
    [HttpGet]
    public IActionResult Register() => User.Identity?.IsAuthenticated == true ? RedirectToAction("Index", "Chat") : View(new RegisterViewModel());

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Chat");
        if (!ModelState.IsValid) return View(model);
        var normalizedName = model.UserName.ToUpperInvariant();
        if (await database.Users.AnyAsync(user => user.NormalizedUserName == normalizedName))
        {
            ModelState.AddModelError(nameof(model.UserName), "Пользователь с таким именем уже существует");
            return View(model);
        }
        var user = new AppUser { UserName = model.UserName, NormalizedUserName = normalizedName };
        user.PasswordHash = passwordHasher.HashPassword(user, model.Password);
        database.Users.Add(user);
        try
        {
            await database.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError("", "Не удалось зарегистрироваться, попробуйте другое имя");
            return View(model);
        }
        await SignInUserAsync(user);
        TempData["Success"] = "Регистрация завершена, вы вошли в чат";
        return RedirectToAction("Index", "Chat");
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Chat");
        return View(new LoginViewModel { ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null });
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Chat");
        if (!ModelState.IsValid) return View(model);
        var normalizedName = model.UserName.ToUpperInvariant();
        var user = await database.Users.SingleOrDefaultAsync(user => user.NormalizedUserName == normalizedName);
        if (user is null || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password) == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError("", "Неверное имя пользователя или пароль");
            return View(model);
        }
        await SignInUserAsync(user);
        TempData["Success"] = "Вход выполнен";
        if (Url.IsLocalUrl(model.ReturnUrl)) return LocalRedirect(model.ReturnUrl!);
        return RedirectToAction("Index", "Chat");
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        sessions.Remove(User.FindFirstValue("SessionId"));
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["Success"] = "Вы вышли из аккаунта";
        return RedirectToAction("Index", "Home");
    }

    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    private async Task SignInUserAsync(AppUser user)
    {
        sessions.Remove(User.FindFirstValue("SessionId"));
        var expiresAt = DateTimeOffset.UtcNow.AddHours(2);
        var sessionId = sessions.Create(expiresAt);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.UserName),
            new Claim("SessionId", sessionId)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity),
            new AuthenticationProperties { ExpiresUtc = expiresAt, IsPersistent = false, AllowRefresh = false });
    }
}
