using AtsManager.Models;
using AtsManager.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// =====================================================
// CONFIGURACIÓN DE BASE DE DATOS
// =====================================================

var connectionString = builder.Configuration.GetConnectionString("ATS_DB_Connection")
    ?? throw new InvalidOperationException(
        "Connection string 'ATS_DB_Connection' not found. Check appsettings.json.");

builder.Services.AddDbContext<AtsDbContext>(options =>
    options.UseSqlServer(
        connectionString,
        sql => sql.EnableRetryOnFailure()
    )
);

// =====================================================
// CONFIGURACIÓN DE SERVICIOS PROPIOS
// =====================================================

var ruc = builder.Configuration["Contribuyente:Ruc"];
var razonSocial = builder.Configuration["Contribuyente:RazonSocial"];

if (string.IsNullOrWhiteSpace(ruc) || string.IsNullOrWhiteSpace(razonSocial))
{
    throw new InvalidOperationException(
        "Contribuyente:Ruc o Contribuyente:RazonSocial no configurados correctamente.");
}

// ATS XML Generator (usa parámetros)
builder.Services.AddScoped<ATSXmlGenerator>(_ =>
    new ATSXmlGenerator(ruc, razonSocial)
);

// Importador de XML
builder.Services.AddScoped<XmlBatchImporter>();

// =====================================================
// RAZOR PAGES + SESSION + SEGURIDAD
// =====================================================

builder.Services.AddRazorPages();

builder.Services.AddHttpContextAccessor();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

// =====================================================
// BUILD APP
// =====================================================

var app = builder.Build();

// =====================================================
// MIDDLEWARE PIPELINE
// =====================================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();
app.UseSession();

app.MapRazorPages();

app.Run();
