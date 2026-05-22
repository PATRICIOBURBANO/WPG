using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AtsManager.Services;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using System.IO;
using AtsManager.Pages.Empresas.Models;


namespace AtsManager.Pages.Reportes
{

    public class ExportarModel : PageModel
    {

        private readonly AtsDbContext _db;
        private readonly ATSXmlGenerator _xmlGenerator;

        [BindProperty]
        public int Anio { get; set; } = DateTime.Now.Year;

        [BindProperty]
        public int Mes { get; set; } = DateTime.Now.Month;

        public string EmpresaNombre { get; set; } = string.Empty;
        public string EmpresaRuc { get; set; } = string.Empty;

        public string Mensaje { get; set; } = string.Empty;

        public ExportarModel(AtsDbContext db, ATSXmlGenerator xmlGenerator)
        {
            _db = db;
            _xmlGenerator = xmlGenerator;
        }

        public async Task OnGetAsync(int? anio, int? mes)
        {
            ViewData["Title"] = "Exportar Anexo Transaccional Simplificado";
            
            if (anio.HasValue) Anio = anio.Value;
            if (mes.HasValue) Mes = mes.Value;
            
            // Usar empresa de la sesión
            var rucEmpresa = HttpContext.Session.GetString("EmpresaSeleccionada");
            if (!string.IsNullOrEmpty(rucEmpresa))
            {
                var empresa = await _db.Empresas.FirstOrDefaultAsync(e => e.Ruc == rucEmpresa);
                if (empresa != null)
                {
                    EmpresaNombre = empresa.RazonSocial;
                    EmpresaRuc = empresa.Ruc;
                }
            }
        }

        public async Task<IActionResult> OnPostExportarAsync(int anio, int mes)
        {
            try
            {
                // Usar empresa de la sesión
                var rucEmpresa = HttpContext.Session.GetString("EmpresaSeleccionada");
                if (string.IsNullOrEmpty(rucEmpresa))
                {
                    Mensaje = "ERROR: No hay empresa seleccionada.";
                    return Page();
                }

                var empresa = await _db.Empresas.FirstOrDefaultAsync(e => e.Ruc == rucEmpresa);
                if (empresa == null)
                {
                    Mensaje = "ERROR: La empresa seleccionada no existe.";
                    return Page();
                }

                var rucFiltro = empresa.Ruc;
                var razonSocial = empresa.RazonSocial;

                var comprasQuery = _db.Compras
                    .Where(c => c.Anio == Anio && c.Mes == Mes);

                var ventasQuery = _db.Ventas
                    .Where(v => v.Anio == Anio && v.Mes == Mes);

                var retencionesQuery = _db.RetencionesClientes
                    .Where(r => r.Anio == Anio && r.Mes == Mes);

                // Filtrar por empresa
                comprasQuery = comprasQuery.Where(c => c.RucEmpresa == rucFiltro);
                ventasQuery = ventasQuery.Where(v => v.RucEmpresa == rucFiltro);
                retencionesQuery = retencionesQuery.Where(r => r.RucEmpresa == rucFiltro);

                var compras = await comprasQuery.ToListAsync();
                var ventas = await ventasQuery.ToListAsync();
                var retenciones = await retencionesQuery.ToListAsync();

                if (!compras.Any() && !ventas.Any() && !retenciones.Any())
                {
                    Mensaje = $"ADVERTENCIA: No se encontraron registros para el período {Mes:D2}/{Anio}.";
                    await OnGetAsync(Anio, Mes);
                    return Page();
                }

                string xmlContent = _xmlGenerator.GenerarXmlString(
                    Mes,
                    Anio,
                    compras,
                    ventas,
                    retenciones,
                    rucFiltro,
                    razonSocial
                );

                string nombreArchivo = $"ATS{Anio}{Mes:D2}{rucFiltro}.xml";

                return File(
                    System.Text.Encoding.UTF8.GetBytes(xmlContent),
                    "application/xml",
                    nombreArchivo
                );
            }
            catch (Exception ex)
            {
                Mensaje = $"ERROR: {ex.Message}";
                await OnGetAsync(Anio, Mes);
                return Page();
            }
        }

    }
}
