using AtsManager.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;

namespace AtsManager.Pages.ImportarXML
{
    public class IndexModel : PageModel
    {
        private readonly XmlBatchImporter _importer;

        [BindProperty]
        public string RutaCarpeta { get; set; } = @"C:\SRI_XMLs\Recibidos"; // Valor por defecto sugerido

        public List<string> MensajesResultado { get; set; } = new List<string>();

        public IndexModel(XmlBatchImporter importer)
        {
            _importer = importer;
        }

        public void OnGet()
        {
        }

        public IActionResult OnPostImportar()
        {
            if (string.IsNullOrEmpty(RutaCarpeta))
            {
                MensajesResultado.Add("ERROR: Debe especificar la ruta de la carpeta.");
                return Page();
            }

            // Llamar al servicio principal
            MensajesResultado = _importer.ImportarDesdeCarpeta(RutaCarpeta);

            return Page();
        }
    }
}