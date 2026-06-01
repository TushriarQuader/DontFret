using DontFret.Data;
using DontFret.Models;
using DontFret.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using System.Diagnostics;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder( args );
// Add services to the container.
builder.Services.Configure<SmtpOptions>( builder.Configuration.GetSection( "Smtp" ) );
builder.Services.AddTransient<IEmailSender<User>, EmailSender>();
builder.Services.AddTransient<IEmailService>( sp => (EmailSender)sp.GetRequiredService<IEmailSender<User>>() );
var connectionString = builder.Configuration.GetConnectionString( "DefaultConnection" ) ?? throw new InvalidOperationException( "Connection string 'DefaultConnection' not found." );
builder.Services.AddDbContext<ApplicationDbContext>( options =>
    options.UseSqlServer( connectionString ) );
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<User>( options => options.SignIn.RequireConfirmedAccount = true )
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddControllersWithViews();
// Configure Stripe
builder.Services.Configure<StripeSettings>( builder.Configuration.GetSection( "Stripe" ) );
Stripe.StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];
builder.Services.AddScoped<StripePaymentService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<WishlistAlertService>();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();
// DB Seeding: In Debug build, database is deleted and created fresh. Doing this via conditional compile tags
#if DEBUG
using var scope = app.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
db.Database.EnsureDeleted();
db.Database.Migrate();
DbInitialiser.Seed( scope.ServiceProvider );
// This launches the email server so we can see incoming emails on "http://localhost:5000"
var toolsDir = Path.Combine(
    Environment.GetFolderPath( Environment.SpecialFolder.UserProfile ),
    ".dotnet", "tools" );
var smtp4devPath = Path.Combine( toolsDir, "smtp4dev.cmd" );

try
{
    Process.Start( new ProcessStartInfo
    {
        FileName = smtp4devPath,
        Arguments = "--smtpport 2525",
        UseShellExecute = true,
        WindowStyle = ProcessWindowStyle.Minimized
    });
}
catch
{
    // smtp4dev may already be running — that's fine
}

#else
using var scope = app.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
db.Database.Migrate();
#endif

// Configure the HTTP request pipeline.
if( app.Environment.IsDevelopment() )
    app.UseMigrationsEndPoint();
else
{
    app.UseExceptionHandler( "/Home/Error" );
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();// need to authenticate before authorizing, otherwise it will not work and will just redirect to the login page without showing the error message
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}" );
app.MapRazorPages();


app.Run();
