using Dejan_Camilleri_SWD63B.DataAccess;
using Dejan_Camilleri_SWD63B.Interfaces;
using Dejan_Camilleri_SWD63B.Services;
using Google.Cloud.Logging.V2;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

//Set up Google Application Credentials for ADC
Environment.SetEnvironmentVariable(
    "GOOGLE_APPLICATION_CREDENTIALS",
    builder.Configuration["Authentication:Google:ServiceAccountCredentials"]
);

//Configure Cloud Logging
builder.Configuration["GoogleCloud:ProjectId"] = builder.Configuration["Authentication:Google:ProjectId"] ?? "620707456996";
builder.Configuration["GoogleCloud:LogName"] = builder.Configuration["GoogleCloud:LogName"] ?? "social-media-app-log";
var loggerFactory = LoggerFactory.Create(logging => {
    logging.AddConsole();
    logging.AddDebug();
});
var cloudLoggingService = new GoogleCloudLoggingService(
    builder.Configuration,
    loggerFactory.CreateLogger<GoogleCloudLoggingService>(),
    builder.Environment
);
builder.Services.AddSingleton<ICloudLoggingService>(cloudLoggingService);

//Initialize and load secrets via Secret Manager
var secretManager = new GoogleSecretManagerService(
    builder.Configuration["GoogleCloud:ProjectId"]!,
    cloudLoggingService
);
await secretManager.LoadSecretsIntoConfigurationAsync(builder.Configuration);
builder.Services.AddSingleton<ISecretManagerService>(secretManager);

//Configure Authentication with Google OAuth
builder.Services.AddAuthentication(options => {
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
})
.AddCookie()
.AddGoogle(options => {
    var googleAuth = builder.Configuration.GetSection("Authentication:Google");
    options.ClientId = googleAuth["ClientId"]!;
    options.ClientSecret = googleAuth["ClientSecret"]!;
    options.Scope.Add("profile");
    options.CallbackPath = "/signin-google";
    options.Events.OnCreatingTicket = context => {
        var email = context.User.GetProperty("email").GetString();
        var picture = context.User.GetProperty("picture").GetString();
        context.Identity.AddClaim(new Claim("email", email!));
        context.Identity.AddClaim(new Claim("picture", picture!));
        return Task.CompletedTask;
    };
});

//Register application services
builder.Services.AddScoped<FirestoreRepository>();
builder.Services.AddScoped<IFileUploadService, FileUploadService>();

// Add Pub/Sub service
builder.Services.AddSingleton<IPubSubService, PubSubService>();

builder.Services.AddStackExchangeRedisCache(opt => {
    opt.Configuration = builder.Configuration["Redis:ConnectionString"];
});
builder.Services.AddSingleton<ICacheService, RedisCacheService>();

//Add MVC
builder.Services.AddControllersWithViews();

var app = builder.Build();

//Configure middleware pipeline
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



/*
 ToDo:
    - Implement roles and page authorizetions
    - Fix images to show in the ticket list


- images stored in a bucket allows only a selection of email addresses/users to access them (User who opened the 	ticket and all it)
- User permissions/roles


- SE4.6:d) Also it should send an email (tip: Use MailGun) to the technicians about this ticket; [2]
	Sending email will also be assessed in KU3.1.
	e) Log into Google Cloud Logging any emails sent while using the 







- CHECK IF DONE: Two Authorization attributes must be applied and working: one on the Users’ controller and
		one on the Technicians’ Controller to allow the respective roles in [1]



 */