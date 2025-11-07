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
    o.MultipartBodyLengthLimit = 1024L * 1024L * 100L; // 100 MB uploads
});

// Code Runner (Judge0 + fallback)
builder.Services.AddHttpClient<Judge0CodeRunner>();
builder.Services.AddScoped<ICodeRunner>(sp =>
{
    var judge = sp.GetRequiredService<Judge0CodeRunner>();
    return judge.IsEnabled ? judge : new NoopCodeRunner();
});

// --- Identity Cookie Settings ---
// Keep Admin using your combined login (/Account/Login). Students will use /Student/Account/Login via links.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/Login";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
});

// --- Authorization Policies ---
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("IsAdmin", p => p.RequireRole("Admin", "Faculty", "Examiner", "Moderator"));
});

// Add a separate cookie just for Students
builder.Services.AddAuthentication()
    .AddCookie("StudentCookie", options =>
    {
        options.LoginPath = "/Student/Account/Login";
        options.AccessDeniedPath = "/Student/Account/Login";
        options.Cookie.Name = "Student.Auth";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

// --- Identity (register ONCE; includes Roles) ---
// IMPORTANT: Relax password requirements so roll numbers (e.g., 21ECE0012) are valid passwords.
builder.Services
    .AddDefaultIdentity<IdentityUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;

        // Allow roll numbers as passwords
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequiredUniqueChars = 1;
        options.Password.RequiredLength = 4; // set higher if your rolls are longer (e.g., 6 or 8)
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// --- MVC & Services ---
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<Quiz_Application_College.Services.Student.AvailableQuizService>();
builder.Services.AddScoped<Quiz_Application_College.Services.Reports.ExportService>();

var app = builder.Build();

// --- Seed roles & users (runs once at startup) ---
await IdentitySeed.SeedAsync(app.Services);

// --- Demo data only in Development ---
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

// Authentication BEFORE Authorization
app.UseAuthentication();
app.UseAuthorization();

// --- Endpoints ---
// Areas: /Admin/... and /Student/...
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

// Default MVC route -> Dashboard
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

// Identity UI pages
app.MapRazorPages();

app.Run();
