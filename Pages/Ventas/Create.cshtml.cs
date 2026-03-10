// Archivo: Pages/Ventas/Create.cshtml.cs

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AtsManager.Models;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading.Tasks;

namespace AtsManager.Pages.Ventas
{
    public class CreateVentaModel : PageModel
    {
        private readonly AtsDbContext _db;

        [BindProperty]
        public Venta RegistroVenta { get; set; } = new Venta();

        [BindProperty]
        [Required(ErrorMessage = "Seleccione una empresa.")]
        public int EmpresaId { get; set; }

        public List<Empresa> Empresas { get; set; } = new List<Empresa>();

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

        public async Task<IActionResult> OnGetAsync()
        {
            Empresas = await _db.Empresas.Where(e => e.Activa).OrderBy(e => e.RazonSocial).ToListAsync();
            
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
                    ModelState.AddModelError("AutorizacionInput", "La Clave de Acceso debe ser de 49 d�gitos.");
                }
                return Page();
            }

            if (!string.IsNullOrEmpty(AutorizacionInput) && AutorizacionInput.Length >= 49)
            {
                try
                {
                    // 1. FECHA DE EMISI�N (Posici�n 0, 6 d�gitos: DDMMAA)
                    string fechaString = AutorizacionInput.Substring(0, 6);
                    DateTime fechaEmision = DateTime.ParseExact(fechaString, "ddMMyy", CultureInfo.InvariantCulture);

                    // 2. TIPO DE COMPROBANTE (Posici�n 8, 2 d�gitos)
                    string tipoComprobante = AutorizacionInput.Substring(8, 2);

                    // 3. RUC PROVEEDOR/CLIENTE (Posici�n 10, 13 d�gitos)
                    string rucCliente = AutorizacionInput.Substring(10, 13);

                    // 4. SERIE (Posici�n 23, 6 d�gitos: EEEPPP)
                    string serieCompleta = AutorizacionInput.Substring(23, 6);

                    // 5. SECUENCIAL (Posici�n 29, 9 d�gitos)
                    string secuencial = AutorizacionInput.Substring(29, 9);

                    // 6. N�MERO CONSOLIDADO (Serie + Secuencial)
                    string numComprobante = serieCompleta + secuencial;

                    // Actualizar el modelo de la p�gina
                    RegistroVenta.Anio = (short)fechaEmision.Year;
                    RegistroVenta.Mes = (short)fechaEmision.Month;
                    RegistroVenta.FechaEmision = fechaEmision;

                    // RUC y Tipo ID
                    RegistroVenta.IdCliente = rucCliente;
                    RegistroVenta.TipoIdCliente = "04"; // Asumimos RUC

                    // Comprobante
                    RegistroVenta.TipoComprobante = tipoComprobante;
                    RegistroVenta.NumComprobante = numComprobante;

                    ModelState.AddModelError(string.Empty, "Datos de comprobante precargados con �xito. Complete Raz�n Social y valores.");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("AutorizacionInput", $"Error al procesar la clave: {ex.Message}");
                }
            }

            // Regresar a la vista para que el usuario complete el formulario
            return Page();
        }

        // --- MANEJADOR DE ENV�O FINAL (OnPostAsync) ---
        public async Task<IActionResult> OnPostAsync()
        {
            ModelState.Remove("RegistroVenta.CargaLote");
            ModelState.Remove("RegistroVenta.CargaLoteId");

            Empresas = await _db.Empresas.Where(e => e.Activa).OrderBy(e => e.RazonSocial).ToListAsync();

            if (EmpresaId <= 0)
            {
                ModelState.AddModelError("EmpresaId", "Seleccione una empresa.");
                return Page();
            }

            var empresa = await _db.Empresas.FindAsync(EmpresaId);
            if (empresa == null)
            {
                ModelState.AddModelError("EmpresaId", "La empresa seleccionada no existe.");
                return Page();
            }

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
            RegistroVenta.RucEmpresa = empresa.Ruc;

            _db.Ventas.Add(RegistroVenta);
            await _db.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}