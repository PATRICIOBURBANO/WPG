// Archivo: Pages/Compras/Create.cshtml.cs

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AtsManager.Models;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AtsManager.Pages.Compras
{
    public class CreateCompraModel : PageModel
    {
        private readonly AtsDbContext _db;

        // Note: Inicializamos la propiedad para evitar advertencias de C#
        [BindProperty]
        public Compra RegistroCompra { get; set; } = new Compra();

        [BindProperty]
        [StringLength(49, MinimumLength = 49, ErrorMessage = "La Clave de Acceso debe tener 49 dígitos.")]
        [RegularExpression(@"^\d{49}$", ErrorMessage = "La Clave de Acceso solo puede contener números.")]
        public string? AutorizacionInput { get; set; }

        public List<string> TiposId { get; set; } = new List<string> { "01", "02", "03", "07", "08" };
        public List<string> TiposComprobante { get; set; } = new List<string> { "01", "04", "05", "41" };

        public CreateCompraModel(AtsDbContext context)
        {
            _db = context;
        }

        public IActionResult OnGet()
        {
            if (RegistroCompra.Id == 0 && string.IsNullOrEmpty(RegistroCompra.Autorizacion))
            {
                RegistroCompra = new Compra
                {
                    FechaEmision = DateTime.Now.Date,
                    TipoComprobante = "01",
                    TipoIdProveedor = "04",
                    Anio = (short)DateTime.Now.Year,
                    Mes = (short)DateTime.Now.Month
                };
            }
            ViewData["Title"] = "Ingreso de Nueva Compra";
            return Page();
        }

        // --- MANEJADOR CORREGIDO PARA BUSCAR DATOS POR CLAVE DE ACCESO ---
        public IActionResult OnPostBuscarComprobante()
        {
            // Resetear el estado del modelo para que la precarga funcione
            ModelState.Clear();

            if (string.IsNullOrEmpty(AutorizacionInput) || AutorizacionInput.Length != 49 || !Regex.IsMatch(AutorizacionInput, @"^\d{49}$"))
            {
                // Si la clave es inválida, se devuelve el error sin precargar datos
                ModelState.AddModelError("AutorizacionInput", "La Clave de Acceso es inválida o no tiene 49 dígitos.");
                return Page();
            }

            try
            {
                // Lógica de Autollenado:
                string fechaString = AutorizacionInput.Substring(0, 6);
                DateTime fechaEmision = DateTime.ParseExact(fechaString, "ddMMyy", CultureInfo.InvariantCulture);
                string tipoComprobante = AutorizacionInput.Substring(8, 2);
                string rucProveedor = AutorizacionInput.Substring(10, 13);
                string serieCompleta = AutorizacionInput.Substring(23, 6);
                string secuencial = AutorizacionInput.Substring(29, 9);
                string numComprobante = serieCompleta + secuencial;

                // 1. Asignar los datos extraídos a RegistroCompra
                RegistroCompra.Anio = (short)fechaEmision.Year;
                RegistroCompra.Mes = (short)fechaEmision.Month;
                RegistroCompra.FechaEmision = fechaEmision;
                RegistroCompra.FechaRegistro = fechaEmision; // Fecha de Registro Contable

                // 2. Asignar Identificación y Tipo
                RegistroCompra.IdProveedor = rucProveedor;
                RegistroCompra.TipoIdProveedor = "04"; // Asumimos RUC

                // 3. Asignar Comprobante
                RegistroCompra.TipoComprobante = tipoComprobante;
                RegistroCompra.NumComprobante = numComprobante;
                RegistroCompra.Autorizacion = AutorizacionInput;

                // Mantiene el campo Razón Social vacío para que el usuario lo ingrese
                RegistroCompra.RazonSocialProveedor = "";

                ModelState.AddModelError(string.Empty, "Datos de comprobante precargados con éxito. Complete los valores y Razón Social.");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("AutorizacionInput", $"Error fatal al decodificar la clave: {ex.Message}. Verifique el formato DDMMAA.");
            }

            // La clave es que el método devuelve Page() con el modelo actualizado
            return Page();
        }

        // ... (El resto de OnPostAsync, que guarda la compra final, permanece sin cambios) ...

        public async Task<IActionResult> OnPostAsync()
        {
            // Remover las validaciones del objeto CargaLote (sin cambios)
            ModelState.Remove("RegistroCompra.CargaLote");
            ModelState.Remove("RegistroCompra.CargaLoteId");

            if (!ModelState.IsValid)
            {
                return Page();
            }
            // ... (Lógica de validación de cuadratura y guardado) ...

            return RedirectToPage("./Index");
        }
    }
}