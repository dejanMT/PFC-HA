using Dejan_Camilleri_SWD63B.DataAccess;
using Google.Cloud.Firestore.V1;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

Environment.SetEnvironmentVariable(
    "GOOGLE_APPLICATION_CREDENTIALS", 
    builder.Configuration["Authentication:Google:ServiceAccountCredentials"]
);

//Added for google auth.  
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
}).AddCookie()
.AddGoogle(options =>
{
    IConfigurationSection googleAuth = builder.Configuration.GetSection("Authentication:Google");
    options.ClientId = googleAuth["ClientId"];
    options.ClientSecret = googleAuth["ClientSecret"];
    options.Scope.Add("profile");
    options.Events.OnCreatingTicket = (context) =>
    {
        String email = context.User.GetProperty("email").GetString();
        String picture = context.User.GetProperty("picture").GetString();
        context.Identity.AddClaim(new Claim("email", email));
        context.Identity.AddClaim(new Claim("picture", picture));
        return Task.CompletedTask;
    };
});

builder.Services.AddScoped<FirestoreRepository>();
//builder.Services.AddScoped<IFileUploadService, FileUploadService>();

// Add services to the container.  
builder.Services.AddControllersWithViews();

var app = builder.Build();

builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Configure the HTTP request pipeline.  
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.  
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
  name: "default",
  pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
