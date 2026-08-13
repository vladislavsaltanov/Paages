using Paages.Web.Components;
using Paages.Infrastructure.Data;
using Paages.Infrastructure.Services;
using Paages.Web.Services;
using Paages.Web.Interfaces;
using Paages.Domain.Interfaces;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using Paages.Infrastructure.Auth;
using Paages.Infrastructure.Email;
using Microsoft.AspNetCore.Authentication.Google;
using Paages.Domain.Exceptions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(o => o.DetailedErrors = builder.Environment.IsDevelopment());

builder.Services.AddPaagesDatabase(builder.Configuration);

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    }).AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
        options.Events.OnTicketReceived = async context =>
        {
            var googleId = context.Principal!.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var email = context.Principal!.FindFirstValue(ClaimTypes.Email)!;
            var emailVerified = context.Principal!.FindFirstValue("email_verified") == "true";

            var accounts = context.HttpContext.RequestServices.GetRequiredService<UserAccountService>();
            var user = await accounts.FindOrCreateGoogleUserAsync(googleId, email, emailVerified);

            context.Principal = new ClaimsPrincipal(AuthClaimsFactory.Build(user, CookieAuthenticationDefaults.AuthenticationScheme));
        };
    });
;

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddOptions<SmtpOptions>().Bind(builder.Configuration.GetSection("Smtp"));
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<AccountTokenService>();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IUIController, UIController>();
builder.Services.AddScoped<INavigationController, NavigationController>();
builder.Services.AddScoped<UserAccountService>();
builder.Services.AddScoped<NoteService>();
builder.Services.AddScoped<DragDropState>();
builder.Services.AddScoped<AppState>();
builder.Services.AddScoped<ITabsState, TabsState>();
builder.Services.AddScoped<ContextMenuState>();
builder.Services.AddScoped<ConfirmDialogState>();
builder.Services.AddScoped<PublicationService>();
builder.Services.AddScoped<PublishDialogState>();

var app = builder.Build();
app.UseHttpsRedirection();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.MapStaticAssets().AllowAnonymous();

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapPost("/account/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    http.Response.Cookies.Delete("paages_tabs", new CookieOptions { Path = "/" });
    return Results.Redirect("/login");
}).AllowAnonymous();

app.MapGet("/account/login-google", (string? returnUrl) =>
    Results.Challenge(new AuthenticationProperties { RedirectUri = returnUrl ?? "/notes" },
        [GoogleDefaults.AuthenticationScheme])
).AllowAnonymous();

app.MapGet("/account/confirm-email", async (string token, AccountTokenService tokens) =>
{
    try { await tokens.ConfirmEmailAsync(token); return Results.Redirect("/login?confirmed=true"); }
    catch (InvalidAccountTokenException) { return Results.Redirect("/login?confirmed=false"); }
}).AllowAnonymous();

app.MapGet("/account/confirm-email-change", async (string token, AccountTokenService tokens) =>
{
    try { await tokens.ConfirmEmailChangeAsync(token); return Results.Redirect("/login?emailChanged=true"); }
    catch (Exception) { return Results.Redirect("/login?emailChanged=false"); }
}).AllowAnonymous();


app.Run();