using AtsManager.Pages.Empresas.Models;
using AtsManager.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

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

// Obtener configuración del contribuyente
var ruc = builder.Configuration["Contribuyente:Ruc"];
var razonSocial = builder.Configuration["Contribuyente:RazonSocial"];

// Importador de XML
builder.Services.AddScoped<XmlBatchImporter>();

// Generador ATS
builder.Services.AddScoped<ATSXmlGenerator>(sp => 
    new ATSXmlGenerator(ruc!, razonSocial!));

// =====================================================
// RAZOR PAGES + SESSION + SEGURIDAD
// =====================================================

builder.Services.AddRazorPages();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentCompanyService, CurrentCompanyService>();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
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

// app.UseHttpsRedirection();
app.UseStaticFiles();

// Serve SRI PDFs from external directory
var sriPdfPath = @"C:\descargasSRI";
if (Directory.Exists(sriPdfPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(sriPdfPath),
        RequestPath = "/descargas"
    });
}

app.UseSession();

app.UseRouting();

app.UseAntiforgery();

app.UseAuthorization();

// Redirect root to company selection at startup
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/")
    {
        context.Response.Redirect("/Empresas/Select?returnUrl=%2FDashboard");
        return;
    }
    await next();
});

app.MapRazorPages();

app.Run();
