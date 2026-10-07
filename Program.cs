using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using software_elviejomadero.Data;
using software_elviejomadero.Models;

var builder = WebApplication.CreateBuilder(args);

// Soporte automático para el puerto dinámico asignado por Render (variable de entorno PORT)
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// 1. Cadena de conexión y DbContext con SQLite
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Data Source=viejo_madero.db";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

// 2. Configuración de ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Políticas de contraseñas amigables y seguras
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;

    // Validación de usuario
    options.User.RequireUniqueEmail = false;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// 3. Configuración de Cookies de autenticación
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.Name = ".ElViejoMadero.Auth";
});

// 4. Servicios de Aplicación (Clean Architecture / SRP)
builder.Services.AddScoped<software_elviejomadero.Services.Interfaces.IAuthenticationService, software_elviejomadero.Services.Implementations.AuthenticationService>();
builder.Services.AddScoped<software_elviejomadero.Services.Interfaces.IEmployeeService, software_elviejomadero.Services.Implementations.EmployeeService>();
builder.Services.AddScoped<software_elviejomadero.Services.Interfaces.IDishService, software_elviejomadero.Services.Implementations.DishService>();
builder.Services.AddScoped<software_elviejomadero.Services.Interfaces.IOrderService, software_elviejomadero.Services.Implementations.OrderService>();

// 5. Registrar MVC con Vistas y ForwardedHeaders para Reverse Proxy (Render)
builder.Services.AddControllersWithViews();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

// 6. Inicializar Base de Datos, Roles y Datos Iniciales (Seed)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        await DbInitializer.InitializeAsync(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ocurrió un error al inicializar y sembrar la base de datos.");
    }
}

// 7. Pipeline HTTP
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();
