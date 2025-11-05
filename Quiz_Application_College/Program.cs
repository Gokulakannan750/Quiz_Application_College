using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Quiz_Application_College.Data;
using Quiz_Application_College.Services.Coding;

var builder = WebApplication.CreateBuilder(args);

// --- Database ---
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 1024L * 1024L * 100L; // 100 MB
});

// Code Runner (Judge0 + fallback)
builder.Services.AddHttpClient<Quiz_Application_College.Services.Coding.Judge0CodeRunner>();
builder.Services.AddScoped<Quiz_Application_College.Services.Coding.ICodeRunner>(sp =>
{
    var judge = sp.GetRequiredService<Quiz_Application_College.Services.Coding.Judge0CodeRunner>();
    return judge.IsEnabled ? judge : new Quiz_Application_College.Services.Coding.NoopCodeRunner();
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
});

// --- Authorization Policies ---
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("IsAdmin", p => p.RequireRole("Admin", "Faculty", "Examiner", "Moderator"));
});

// --- Identity (register ONCE; includes Roles) ---
// NOTE: If you later add a custom ApplicationUser class, replace IdentityUser with your type.
builder.Services
    .AddDefaultIdentity<IdentityUser>(options =>
    {
        // while developing, you can relax this; set to true when you wire email
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddRoles<IdentityRole>() // REQUIRED for RoleManager and [Authorize(Roles=...)]
    .AddEntityFrameworkStores<ApplicationDbContext>();

// --- MVC ---
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<Quiz_Application_College.Services.Student.AvailableQuizService>();
builder.Services.AddScoped<Quiz_Application_College.Services.Reports.ExportService>();


var app = builder.Build();

// --- Seed roles & users (runs once at startup) ---
await IdentitySeed.SeedAsync(app.Services);

// --- Seed demo data (ONLY in Development environment) ---
await IdentitySeed.SeedAsync(app.Services);
if (app.Environment.IsDevelopment())
{
    await DemoSeed.SeedAsync(app.Services);
}

// --- Pipeline ---
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// IMPORTANT: Authentication BEFORE Authorization
app.UseAuthentication();
app.UseAuthorization();

// --- Endpoints ---
// Areas: /Admin/... and /Student/...
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

// Default MVC route -> go through Dashboard
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

// Identity UI pages (Login/Register/Manage)
app.MapRazorPages();

app.Run();
