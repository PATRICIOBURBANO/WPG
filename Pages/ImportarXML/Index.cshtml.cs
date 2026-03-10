using AtsManager.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
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

        public IndexModel(XmlBatchImporter xmlBatchImporter)
        {
            _xmlBatchImporter = xmlBatchImporter;
        }

        [BindProperty] public int Anio { get; set; }
        [BindProperty] public int Mes { get; set; }
        [BindProperty] public string TipoArchivo { get; set; } = "XML";
        [BindProperty] public string ContextoCarga { get; set; } = "RECIBIDOS";

        [BindProperty]
        public List<IFormFile> XmlFiles { get; set; } = new();

        public List<string> ResultadosImportacion { get; set; } = new();
        public string? MensajeCarga { get; set; }

        public async Task<IActionResult> OnPostAsync()
        {
            if (XmlFiles == null || !XmlFiles.Any())
            {
                MensajeCarga = "No se seleccionaron archivos XML.";
                return Page();
            }

            var tempPath = Path.Combine(
                Path.GetTempPath(),
                $"XML_IMPORT_{Guid.NewGuid()}"
            );

            Directory.CreateDirectory(tempPath);

            foreach (var file in XmlFiles)
            {
                if (!file.FileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    continue;

                var filePath = Path.Combine(tempPath, Path.GetFileName(file.FileName));
                using var stream = new FileStream(filePath, FileMode.Create);
                await file.CopyToAsync(stream);
            }

            ResultadosImportacion = _xmlBatchImporter.ImportarDesdeCarpeta(tempPath, ContextoCarga);
            MensajeCarga = "Proceso finalizado.";

            return Page();
        }
    }
}
