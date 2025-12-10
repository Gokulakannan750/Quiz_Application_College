using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Quiz_Application_College.Data;
using Quiz_Application_College.Services.Coding;

var builder = WebApplication.CreateBuilder(args);

// ---------------- DB ----------------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(opt => opt.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// (optional) larger uploads
builder.Services.Configure<FormOptions>(o => { o.MultipartBodyLengthLimit = 100L * 1024 * 1024; });

// ---------------- Services ----------------
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages(); // Identity UI

// Code runner DI
builder.Services.AddHttpClient<Judge0CodeRunner>();
builder.Services.AddScoped<ICodeRunner>(sp =>
{
    var judge = sp.GetRequiredService<Judge0CodeRunner>();
    return judge.IsEnabled ? judge : new NoopCodeRunner();
});

// Identity (Admin)
builder.Services
    .AddDefaultIdentity<IdentityUser>(opt =>
    {
        opt.SignIn.RequireConfirmedAccount = false;
        opt.Password.RequireDigit = false;
        opt.Password.RequireLowercase = false;
        opt.Password.RequireNonAlphanumeric = false;
        opt.Password.RequireUppercase = false;
        opt.Password.RequiredUniqueChars = 1;
        opt.Password.RequiredLength = 4;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Admin cookie → Identity UI
builder.Services.ConfigureApplicationCookie(opt =>
{
    opt.LoginPath = "/Identity/Account/Login";
    opt.AccessDeniedPath = "/Identity/Account/AccessDenied";
    opt.ReturnUrlParameter = "returnUrl";
    opt.SlidingExpiration = true;
    opt.ExpireTimeSpan = TimeSpan.FromHours(8);
});

// Student cookie (custom)
builder.Services.AddAuthentication()
    .AddCookie("StudentCookie", opt =>
    {
        opt.LoginPath = "/Student/Auth/Login";
        opt.AccessDeniedPath = "/Student/Auth/Denied";
        opt.ReturnUrlParameter = "returnUrl";
        opt.Cookie.Name = ".QuizApp.Student";
        opt.SlidingExpiration = true;
        opt.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

// Authorization policies (optional)
builder.Services.AddAuthorization(options =>
{
    // Simple, clear role policies
    options.AddPolicy("IsAdmin", policy => policy.RequireRole("Admin"));
    options.AddPolicy("IsTrainerOrAdmin", policy => policy.RequireRole("Admin", "Trainer"));
});

// App services
builder.Services.AddScoped<Quiz_Application_College.Services.Student.AvailableQuizService>();
builder.Services.AddScoped<Quiz_Application_College.Services.Reports.ExportService>();

// Antiforgery (for student login form)
builder.Services.AddAntiforgery(o =>
{
    o.Cookie.Name = ".QuizApp.AntiForgery";
});

// ---------------- Build ----------------
var app = builder.Build();

// Seed data
await IdentitySeed.SeedAsync(app.Services);
if (app.Environment.IsDevelopment())
{
    await DemoSeed.SeedAsync(app.Services);
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

app.UseAuthentication();  // before authorization
app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
