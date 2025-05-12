using Dejan_Camilleri_SWD63B.DataAccess;
using Dejan_Camilleri_SWD63B.Interfaces;
using Dejan_Camilleri_SWD63B.Models;
using Dejan_Camilleri_SWD63B.Services;
using Google.Cloud.Firestore;
using Google.Cloud.SecretManager.V1;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

string projectId = "boxwood-night-449813-s9";

builder.Logging.AddConsole();
builder.Logging.AddDebug();

var secretClient = SecretManagerServiceClient.Create();
string FetchSecret(string name) =>
    secretClient
        .AccessSecretVersion(
            new SecretVersionName(projectId, name, "latest"))
        .Payload.Data.ToStringUtf8();

// Configure your Cloud Logging defaults in config
builder.Configuration["GoogleCloud:ProjectId"] = projectId;
builder.Configuration["GoogleCloud:LogName"] = "social-media-app-log";

var loggerFactory = LoggerFactory.Create(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
});
var cloudLoggingService = new GoogleCloudLoggingService(
    builder.Configuration,
    loggerFactory.CreateLogger<GoogleCloudLoggingService>(),
    builder.Environment
);
builder.Services.AddSingleton<ICloudLoggingService>(cloudLoggingService);

await new GoogleSecretManagerService(projectId, cloudLoggingService)
        .LoadSecretsIntoConfigurationAsync(builder.Configuration);

// Authentication configuration
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
})
.AddCookie()
.AddGoogle(o =>
{
    o.ClientId = FetchSecret("clientid");
    o.ClientSecret = FetchSecret("clientSecret");
    o.Scope.Add("profile");
    o.CallbackPath = "/signin-google";

    o.Events.OnCreatingTicket = async ctx =>
    {
        var email = ctx.User.GetProperty("email").GetString()!;
        var repo = ctx.HttpContext.RequestServices.GetRequiredService<FirestoreRepository>();

        var user = await repo.GetUserByEmailAsync(email)
                   ?? new User
                   {
                       Email = email,
                       DisplayName = ctx.Principal.FindFirst(ClaimTypes.Name)?.Value,
                       Role = "User"
                   };
        if (user.Id == null)
            await repo.CreateUserAsync(user);

        // Tear out default sub, re-add your ID and other claims
        var oldSub = ctx.Principal.FindFirst(ClaimTypes.NameIdentifier);
        if (oldSub != null) ctx.Identity.RemoveClaim(oldSub);

        ctx.Identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id!));
        ctx.Identity.AddClaim(new Claim(ClaimTypes.Email, email));
        ctx.Identity.AddClaim(new Claim("picture",
            ctx.User.GetProperty("picture").GetString()!));
        ctx.Identity.AddClaim(new Claim(ClaimTypes.Role, user.Role));
    };
});

builder.Services.AddSingleton(_ => FirestoreDb.Create(projectId));
builder.Services.AddScoped<FirestoreRepository>();
builder.Services.AddScoped<IFileUploadService, FileUploadService>();
builder.Services.AddSingleton<IPubSubService, PubSubService>();
builder.Services.AddStackExchangeRedisCache(opt =>
{
    opt.Configuration = builder.Configuration["Redis:ConnectionString"];
});
builder.Services.AddScoped<ICacheService, RedisCacheService>();

builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

app.Run();
