// Archivo: Pages/Ventas/Create.cshtml.cs

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AtsManager.Models;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AtsManager.Pages.Ventas
{
    public class CreateVentaModel : PageModel
    {
        private readonly AtsDbContext _db;

        [BindProperty]
        public Venta RegistroVenta { get; set; } = new Venta();

        // NUEVO: Propiedad para capturar la Clave de Acceso
        [BindProperty]
        [StringLength(49, MinimumLength = 49, ErrorMessage = "La Clave de Acceso debe tener 49 dígitos.")]
        [RegularExpression(@"^\d{49}$", ErrorMessage = "La Clave de Acceso solo puede contener números.")]
        public string? AutorizacionInput { get; set; }

        // Datos para los campos de selección (sin cambios)
        public List<string> TiposId { get; set; } = new List<string> { "04", "05", "06", "07", "08" };
        public List<string> TiposComprobante { get; set; } = new List<string> { "18", "41", "07" };

        public CreateVentaModel(AtsDbContext context)
        {
            _db = context;
        }

        public IActionResult OnGet()
        {
            if (RegistroVenta.Id == 0)
            {
                RegistroVenta = new Venta
                {
                    FechaEmision = DateTime.Now.Date,
                    TipoComprobante = "18",
                    TipoIdCliente = "04",
                    Anio = (short)DateTime.Now.Year,
                    Mes = (short)DateTime.Now.Month
                };
            }
            ViewData["Title"] = "Ingreso de Nueva Venta";
            return Page();
        }

        // --- MANEJADOR PARA BUSCAR DATOS POR CLAVE DE ACCESO ---
        public IActionResult OnPostBuscarComprobante()
        {
            if (!ModelState.IsValid)
            {
                if (!string.IsNullOrEmpty(AutorizacionInput) && AutorizacionInput.Length != 49)
                {
                    ModelState.AddModelError("AutorizacionInput", "La Clave de Acceso debe ser de 49 dígitos.");
                }
                return Page();
            }

            if (!string.IsNullOrEmpty(AutorizacionInput) && AutorizacionInput.Length >= 49)
            {
                try
                {
                    // 1. FECHA DE EMISIÓN (Posición 0, 6 dígitos: DDMMAA)
                    string fechaString = AutorizacionInput.Substring(0, 6);
                    DateTime fechaEmision = DateTime.ParseExact(fechaString, "ddMMyy", CultureInfo.InvariantCulture);

                    // 2. TIPO DE COMPROBANTE (Posición 8, 2 dígitos)
                    string tipoComprobante = AutorizacionInput.Substring(8, 2);

                    // 3. RUC PROVEEDOR/CLIENTE (Posición 10, 13 dígitos)
                    string rucCliente = AutorizacionInput.Substring(10, 13);

                    // 4. SERIE (Posición 23, 6 dígitos: EEEPPP)
                    string serieCompleta = AutorizacionInput.Substring(23, 6);

                    // 5. SECUENCIAL (Posición 29, 9 dígitos)
                    string secuencial = AutorizacionInput.Substring(29, 9);

                    // 6. NÚMERO CONSOLIDADO (Serie + Secuencial)
                    string numComprobante = serieCompleta + secuencial;

                    // Actualizar el modelo de la página
                    RegistroVenta.Anio = (short)fechaEmision.Year;
                    RegistroVenta.Mes = (short)fechaEmision.Month;
                    RegistroVenta.FechaEmision = fechaEmision;

                    // RUC y Tipo ID
                    RegistroVenta.IdCliente = rucCliente;
                    RegistroVenta.TipoIdCliente = "04"; // Asumimos RUC

                    // Comprobante
                    RegistroVenta.TipoComprobante = tipoComprobante;
                    RegistroVenta.NumComprobante = numComprobante;

                    ModelState.AddModelError(string.Empty, "Datos de comprobante precargados con éxito. Complete Razón Social y valores.");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("AutorizacionInput", $"Error al procesar la clave: {ex.Message}");
                }
            }

            // Regresar a la vista para que el usuario complete el formulario
            return Page();
        }

        // --- MANEJADOR DE ENVÍO FINAL (OnPostAsync) ---
        public async Task<IActionResult> OnPostAsync()
        {
            ModelState.Remove("RegistroVenta.CargaLote");
            ModelState.Remove("RegistroVenta.CargaLoteId");

            if (!ModelState.IsValid)
            {
                return Page();
            }

            if (RegistroVenta.MontoTotal != (RegistroVenta.BaseImponible + RegistroVenta.BaseImpGrav + RegistroVenta.BaseNoGraIva + RegistroVenta.MontoIva + RegistroVenta.MontoIce))
            {
                ModelState.AddModelError("RegistroVenta.MontoTotal", "El Monto Total no cuadra con la suma de las bases imponibles, IVA e ICE.");
                return Page();
            }

            RegistroVenta.CargaLoteId = null;
            RegistroVenta.UsuarioCreacion = User?.Identity?.Name ?? "SistemaManual";
            RegistroVenta.FechaCreacion = DateTime.Now;

            _db.Ventas.Add(RegistroVenta);
            await _db.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}