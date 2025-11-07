// Directivas using necesarias para los modelos, servicios y Entity Framework Core
using AtsManager.Models;
using AtsManager.Services;
using AtsManager.ServicesA;
using Microsoft.AspNetCore.Http; // Necesario para AddHttpContextAccessor y Session
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography.X509Certificates;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// --- CONFIGURACIÓN DE BASE DE DATOS Y SERVICIOS ---

// 1. Obtener la cadena de conexión del appsettings.json
var connectionString = builder.Configuration.GetConnectionString("ATS_DB_Connection") ??
           throw new InvalidOperationException("Connection string 'ATS_DB_Connection' not found. Please check appsettings.json.");

// 2. Registrar el DbContext (Inyección de Dependencias)
builder.Services.AddDbContext<AtsDbContext>(options =>
  options.UseSqlServer(connectionString,
    sqlServerOptions => sqlServerOptions.EnableRetryOnFailure() // Corrección clave
    ));

// 3. Registrar el servicio de Generación XML (ATSXmlGenerator)
var ruc = builder.Configuration["Contribuyente:Ruc"];
var razonSocial = builder.Configuration["Contribuyente:RazonSocial"];

if (string.IsNullOrEmpty(ruc) || string.IsNullOrEmpty(razonSocial))
{
    throw new InvalidOperationException("Contribuyente Ruc o RazonSocial no configurados correctamente en appsettings.json");
}

builder.Services.AddScoped<ATSXmlGenerator>(sp =>
  new ATSXmlGenerator(ruc, razonSocial));

builder.Services.AddScoped<XmlBatchImporter>();
// --- FIN DE CONFIGURACIÓN DE SERVICIOS PROPIOS ---

// Añadir soporte para Razor Pages (Vistas y lógica de UI)
builder.Services.AddRazorPages();
builder.Services.AddControllersWithViews();

// 💥 CONFIGURACIÓN DE MIDDLEWARE REQUERIDO (ADD SERVICES) - UBICACIÓN CORRECTA
builder.Services.AddHttpContextAccessor();

builder.Services.AddSession(options => // Necesario para usar Session
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;

    // -----------------------------------------------------
    // 🎯 CORRECCIÓN PARA EL ERROR DE COOKIE 'SameSite'
    // -----------------------------------------------------
    // Forzar la cookie a enviarse solo en el mismo sitio (Strict es el más seguro)
    options.Cookie.SameSite = SameSiteMode.Strict;
    // Forzar la cookie a enviarse solo sobre HTTPS (resuelve el error de "insecure origin")
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    // -----------------------------------------------------
});

// -----------------------------------------------------
// 🎯 NUEVO BLOQUE: APLICAR LA MISMA POLÍTICA AL ANTIFORGERY TOKEN
// -----------------------------------------------------
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.HttpOnly = true;
});
// -----------------------------------------------------


var app = builder.Build(); // El punto donde la colección de servicios se vuelve de solo lectura.

// Configure the HTTP request pipeline (Configuración del middleware)
if (!app.Environment.IsDevelopment())
{
    // Manejo de errores en producción
    app.UseExceptionHandler("/Error");
    // Habilitar HSTS (medida de seguridad)
    app.UseHsts();
}

// Middleware de seguridad y estáticos
app.UseHttpsRedirection();
app.UseStaticFiles(); // Permite servir archivos de la carpeta wwwroot

app.UseRouting();

// Middleware de autenticación y autorización (si se implementa)
app.UseAuthorization();

// Mover UseSession aquí
app.UseSession();

// Mapear Razor Pages a rutas URL
app.MapRazorPages();

app.Run();