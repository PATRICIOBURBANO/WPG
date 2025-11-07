using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AtsManager.Models;
using AtsManager.Services;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

// Asegúrate de que esta directiva esté incluida
using System.Text;
using AtsManager.ServicesA;

namespace AtsManager.Pages.Reportes
{
    public class ExportarModel : PageModel
    {
        private readonly AtsDbContext _db;
        private readonly ATSXmlGenerator _xmlGenerator;

        // Propiedades para la selección del período por el usuario
        [BindProperty]
        public int Anio { get; set; } = DateTime.Now.Year;

        [BindProperty]
        public int Mes { get; set; } = DateTime.Now.Month;

        public string Mensaje { get; set; } = string.Empty;

        public ExportarModel(AtsDbContext db, ATSXmlGenerator xmlGenerator)
        {
            _db = db;
            _xmlGenerator = xmlGenerator;
        }

        public void OnGet()
        {
            ViewData["Title"] = "Exportar Anexo Transaccional Simplificado";
        }

        public async Task<IActionResult> OnPostExportarAsync()
        {
            if (Anio <= 2000 || Mes < 1 || Mes > 12)
            {
                Mensaje = "ERROR: Seleccione un período fiscal válido.";
                return Page();
            }

            var compras = await _db.Compras
                .Where(c => c.Anio == Anio && c.Mes == Mes)
                .ToListAsync();

            var ventas = await _db.Ventas
                .Where(v => v.Anio == Anio && v.Mes == Mes)
                .ToListAsync();

            if (!compras.Any() && !ventas.Any())
            {
                Mensaje = $"ADVERTENCIA: No se encontraron registros para el período {Mes:D2}/{Anio}.";
                return Page();
            }

            // 1. Generar el XML como STRING (Usando el método GenerarXmlBytes)
            byte[] fileBytes = _xmlGenerator.GenerarXmlBytes(Mes, Anio, compras, ventas);

            // 2. Devolver el XML. No hay manipulación de string, se envían los bytes directos.
            string nombreArchivo = $"ATS{Anio}{Mes:D2}{_xmlGenerator.Ruc}.xml";

            return File(
                fileBytes,
                "application/xml",
                nombreArchivo
            );
        }
    }
}