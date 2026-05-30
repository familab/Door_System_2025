using DoorApp.Familab.Api;
using DoorApp.Familab.Application.Abstractions;
using DoorApp.Familab.Application.Options;
using DoorApp.Familab.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;

var builder = WebApplication.CreateBuilder(args);

// ---- Services -------------------------------------------------------------
builder.Services.AddDoorApp(builder.Configuration);
builder.Services.AddSingleton<AuthThrottle>();

var authOptions = builder.Configuration
    .GetSection(DoorOptions.SectionName)
    .Get<DoorOptions>()?.Auth ?? new AuthOptions();

var authBuilder = builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(Math.Max(1, authOptions.SessionTtlHours));
        options.SlidingExpiration = true;
        options.Cookie.Name = "door_session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

if (authOptions.Google.Enabled && !string.IsNullOrEmpty(authOptions.Google.ClientId))
{
    authBuilder.AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
    {
        options.ClientId = authOptions.Google.ClientId;
        options.ClientSecret = authOptions.Google.ClientSecret;
        options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.CallbackPath = "/signin-google";
        options.Scope.Add("email");
        options.SaveTokens = false;

        // Enforce the email/domain whitelist on the way back from Google.
        options.Events.OnTicketReceived = context =>
        {
            var rules = context.HttpContext.RequestServices.GetRequiredService<IAccessRulesService>();
            var email = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(email) || !rules.IsEmailAllowed(email))
            {
                context.Fail("Email not allowed");
                context.Response.Redirect("/login?error=" + Uri.EscapeDataString("Email not allowed"));
                context.HandleResponse();
            }
            return Task.CompletedTask;
        };
    });
}

builder.Services.AddAuthorization();

var app = builder.Build();

// ---- Pipeline -------------------------------------------------------------
app.UseAuthentication();
app.UseAuthorization();

app.MapDoorEndpoints();

app.Logger.LogInformation("Door Controller (.NET) starting");

app.Run();

// Exposed so the test project's WebApplicationFactory can bootstrap the app.
public partial class Program;
