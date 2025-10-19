// Directivas using necesarias para los modelos, servicios y Entity Framework Core
using AtsManager.Models;
using AtsManager.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography.X509Certificates;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// --- CONFIGURACIÓN DE BASE DE DATOS Y SERVICIOS ---

// 1. Obtener la cadena de conexión del appsettings.json
// El nombre de la conexión es "ATS_DB_Connection" y está en appsettings.json
var connectionString = builder.Configuration.GetConnectionString("ATS_DB_Connection") ??
                       throw new InvalidOperationException("Connection string 'ATS_DB_Connection' not found. Please check appsettings.json.");

// 2. Registrar el DbContext (Inyección de Dependencias)
// Esto permite que el DbContext sea inyectado en las Razor Pages y otros servicios.
builder.Services.AddDbContext<AtsDbContext>(options =>
    options.UseSqlServer(connectionString,
        sqlServerOptions => sqlServerOptions.EnableRetryOnFailure() // <--- CORRECCIÓN CLAVE
    ));

// 3. Registrar el servicio de Generación XML (ATSXmlGenerator)
// Se registra como Scoped y se inicializa con los datos del contribuyente 
// obtenidos del appsettings.json.
var ruc = builder.Configuration["Contribuyente:Ruc"];
var razonSocial = builder.Configuration["Contribuyente:RazonSocial"];

if (string.IsNullOrEmpty(ruc) || string.IsNullOrEmpty(razonSocial))
{
    throw new InvalidOperationException("Contribuyente Ruc o RazonSocial no configurados correctamente en appsettings.json");
}

// 1. Registro ÚNICO del ATSXmlGenerator
builder.Services.AddScoped<ATSXmlGenerator>(sp =>
    new ATSXmlGenerator(ruc, razonSocial));

// 2. Registro del XmlBatchImporter (¡Correcto!)
// Este servicio se inyectará correctamente porque ATSXmlGenerator ya fue definido.
builder.Services.AddScoped<XmlBatchImporter>();
// --- FIN DE CONFIGURACIÓN ---

// Añadir soporte para Razor Pages (Vistas y lógica de UI)
builder.Services.AddRazorPages();

var app = builder.Build();

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

// Configuración de rutas
app.UseRouting();

// Middleware de autenticación y autorización (si se implementa)
app.UseAuthorization();

// Mapear Razor Pages a rutas URL
app.MapRazorPages();

app.Run();
