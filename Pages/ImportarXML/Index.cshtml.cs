using AtsManager.Pages.Empresas.Models;
using AtsManager.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AtsManager.Pages.ImportarXML
{
    public class IndexModel : PageModel
    {
        private readonly XmlBatchImporter _xmlBatchImporter;
        private readonly AtsDbContext _db;

        public IndexModel(XmlBatchImporter xmlBatchImporter, AtsDbContext db)
        {
            _xmlBatchImporter = xmlBatchImporter;
            _db = db;
        }

        [BindProperty] public int Anio { get; set; }
        [BindProperty] public int Mes { get; set; }
        [BindProperty] public string TipoArchivo { get; set; } = "XML";
        [BindProperty] public string ContextoCarga { get; set; } = "EMITIDOS";

        [BindProperty]
        public List<IFormFile> XmlFiles { get; set; } = new();

        public List<string> ResultadosImportacion { get; set; } = new();
        public string? MensajeCarga { get; set; }
        
        public string EmpresaNombre { get; set; } = string.Empty;
        public string EmpresaRuc { get; set; } = string.Empty;

        public async Task OnGetAsync()
        {
            var hoy = DateTime.Now;
            Anio = hoy.Year;
            Mes = hoy.Month == 1 ? 12 : hoy.Month - 1;
            if (Mes == 12) Anio = hoy.Year - 1;
            
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

        public async Task<IActionResult> OnPostAsync()
        {
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
            
            if (string.IsNullOrEmpty(rucEmpresa))
            {
                MensajeCarga = "No hay empresa seleccionada. Por favor seleccione una empresa primero.";
                return Page();
            }
            
            if (XmlFiles == null || !XmlFiles.Any())
            {
                MensajeCarga = "No se seleccionaron archivos XML.";
                return Page();
            }

            var tempPath = Path.Combine(Path.GetTempPath(), $"XML_IMPORT_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempPath);

            foreach (var file in XmlFiles)
            {
                if (!file.FileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    continue;

                var filePath = Path.Combine(tempPath, Path.GetFileName(file.FileName));
                using var stream = new FileStream(filePath, FileMode.Create);
                await file.CopyToAsync(stream);
            }

            ResultadosImportacion = _xmlBatchImporter.ImportarDesdeCarpeta(tempPath, ContextoCarga, rucEmpresa);
            MensajeCarga = "Proceso finalizado.";

            return Page();
        }
    }
}
