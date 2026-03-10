using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AtsManager.Models;
using AtsManager.Services;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;


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

        [BindProperty]
        public int? EmpresaId { get; set; }

        [BindProperty]
        public string? RucOverride { get; set; }

        [BindProperty]
        public string? RazonSocialOverride { get; set; }

        public List<Empresa> Empresas { get; set; } = new List<Empresa>();

        public string Mensaje { get; set; } = string.Empty;

        public ExportarModel(AtsDbContext db, ATSXmlGenerator xmlGenerator)
        {
            _db = db;
            _xmlGenerator = xmlGenerator;
        }

        public async Task OnGetAsync()
        {
            ViewData["Title"] = "Exportar Anexo Transaccional Simplificado";
            Empresas = await _db.Empresas.Where(e => e.Activa).OrderBy(e => e.RazonSocial).ToListAsync();
        }
        public async Task<IActionResult> OnPostExportarAsync()
        {
            ViewData["Title"] = "Exportar Anexo Transaccional Simplificado";
            Empresas = await _db.Empresas.Where(e => e.Activa).OrderBy(e => e.RazonSocial).ToListAsync();

            if (Anio <= 2000 || Mes < 1 || Mes > 12)
            {
                Mensaje = "ERROR: Seleccione un per�odo fiscal v�lido.";
                return Page();
            }

            string? rucFiltro = null;
            if (EmpresaId.HasValue)
            {
                var empresa = await _db.Empresas.FindAsync(EmpresaId.Value);
                if (empresa != null)
                {
                    RucOverride = empresa.Ruc;
                    RazonSocialOverride = empresa.RazonSocial;
                    rucFiltro = empresa.Ruc;
                }
            }

            var comprasQuery = _db.Compras
                .Where(c => c.Anio == Anio && c.Mes == Mes);

            var ventasQuery = _db.Ventas
                .Where(v => v.Anio == Anio && v.Mes == Mes);

            if (!string.IsNullOrEmpty(rucFiltro))
            {
                comprasQuery = comprasQuery.Where(c => c.RucEmpresa == rucFiltro);
                ventasQuery = ventasQuery.Where(v => v.RucEmpresa == rucFiltro);
            }

            var compras = await comprasQuery.ToListAsync();
            var ventas = await ventasQuery.ToListAsync();

            if (!compras.Any() && !ventas.Any())
            {
                Mensaje = $"ADVERTENCIA: No se encontraron registros para el per�odo {Mes:D2}/{Anio}.";
                return Page();
            }

            byte[] fileBytes = _xmlGenerator.GenerarXmlBytes(
                Mes,
                Anio,
                compras,
                ventas,
                RucOverride,
                RazonSocialOverride
            );

            string rucFinal = !string.IsNullOrWhiteSpace(RucOverride)
                ? RucOverride
                : _xmlGenerator.Ruc;

            string nombreArchivo = $"ATS{Anio}{Mes:D2}{rucFinal}.xml";

            return File(
                fileBytes,
                "application/xml",
                nombreArchivo
            );
        }

    }
}