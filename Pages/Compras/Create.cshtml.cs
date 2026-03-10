// Archivo: Pages/Compras/Create.cshtml.cs

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AtsManager.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;

// 🚨 CORRECCIÓN 1: Añadir directiva para las anotaciones de datos (StringLength, RegularExpression)
using System.ComponentModel.DataAnnotations;

namespace AtsManager.Pages.Compras
{
    public class CreateCompraModel : PageModel
    {
        private readonly AtsDbContext _db;

        [BindProperty]
        public Compra RegistroCompra { get; set; } = new Compra();

        [BindProperty]
        [Required(ErrorMessage = "Seleccione una empresa.")]
        public int EmpresaId { get; set; }

        public List<Empresa> Empresas { get; set; } = new List<Empresa>();

        [BindProperty]
        // Las validaciones de StringLength y RegularExpression ahora funcionan gracias al using anterior.
        [StringLength(49, MinimumLength = 49, ErrorMessage = "La Clave de Acceso debe tener 49 dígitos.")]
        [RegularExpression(@"^\d{49}$", ErrorMessage = "La Clave de Acceso solo puede contener números.")]
        public string? AutorizacionInput { get; set; }

        public List<string> TiposId { get; set; } = new List<string> { "01", "02", "03", "07", "08" };
        public List<string> TiposComprobante { get; set; } = new List<string> { "01", "04", "05", "41" };

        public CreateCompraModel(AtsDbContext context)
        {
            _db = context;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            Empresas = await _db.Empresas.Where(e => e.Activa).OrderBy(e => e.RazonSocial).ToListAsync();
            
            if (RegistroCompra.Id == 0)
            {
                RegistroCompra = new Compra
                {
                    FechaEmision = DateTime.Now.Date,
                    TipoComprobante = "01",
                    TipoIdProveedor = "01",
                    Anio = (short)DateTime.Now.Year,
                    Mes = (short)DateTime.Now.Month,

                    // Predefinidos en Cero
                    BaseImpGrav = 0.00M,
                    BaseNoGraIva = 0.00M,
                    BaseImponible = 0.00M,
                    MontoIva = 0.00M,
                    MontoIce = 0.00M,
                    MontoTotal = 0.00M
                };
            }
            ViewData["Title"] = "Ingreso de Nueva Compra";
            return Page();
        }

        public IActionResult OnPostBuscarComprobante()
        {
            ModelState.Clear();

            // 1. Manejar clave no electrónica (10 dígitos)
            string clave = AutorizacionInput?.Trim() ?? string.Empty;
            if (clave.Length == 10)
            {
                if (!Regex.IsMatch(clave, @"^\d{10}$"))
                {
                    ModelState.AddModelError("AutorizacionInput", "La Clave de 10 dígitos debe contener solo números.");
                    return Page();
                }
                RegistroCompra.Autorizacion = clave;
                ModelState.AddModelError(string.Empty, "Clave de 10 dígitos aceptada. Digite el resto de los datos manualmente.");
                return Page();
            }

            // 2. Manejar clave electrónica (49 dígitos)
            if (clave.Length != 49 || !Regex.IsMatch(clave, @"^\d{49}$"))
            {
                ModelState.AddModelError("AutorizacionInput", "La Clave de Acceso debe tener 10 o 49 dígitos.");
                return Page();
            }

            try
            {
                // Lógica de precarga
                string fechaString = clave.Substring(0, 6);
                DateTime fechaEmision = DateTime.ParseExact(fechaString, "ddMMyy", CultureInfo.InvariantCulture);
                string tipoComprobante = clave.Substring(8, 2);
                string rucProveedor = clave.Substring(10, 13);
                string serieCompleta = clave.Substring(23, 6);
                string secuencial = clave.Substring(29, 9);
                string numComprobante = serieCompleta + secuencial;

                RegistroCompra.Anio = (short)fechaEmision.Year;
                RegistroCompra.Mes = (short)fechaEmision.Month;
                RegistroCompra.FechaEmision = fechaEmision;
                RegistroCompra.FechaRegistro = fechaEmision;
                RegistroCompra.IdProveedor = rucProveedor;
                RegistroCompra.TipoIdProveedor = (rucProveedor.Length == 13) ? "01" : "02";
                RegistroCompra.TipoComprobante = tipoComprobante;
                RegistroCompra.NumComprobante = numComprobante;
                RegistroCompra.Autorizacion = clave;
                RegistroCompra.RazonSocialProveedor = "";

                ModelState.AddModelError(string.Empty, "Datos de comprobante precargados con éxito.");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("AutorizacionInput", $"Error al procesar la clave electrónica: {ex.Message}");
            }
            return Page();
        }

        [BindProperty]
        public string FechaEmisionInput { get; set; }

        public async Task<IActionResult> OnPostAsync()
        {
            Empresas = await _db.Empresas.Where(e => e.Activa).OrderBy(e => e.RazonSocial).ToListAsync();

            if (EmpresaId <= 0)
            {
                ModelState.AddModelError("EmpresaId", "Seleccione una empresa.");
                return RetornarConErroresDeDiagnostico();
            }

            var empresa = await _db.Empresas.FindAsync(EmpresaId);
            if (empresa == null)
            {
                ModelState.AddModelError("EmpresaId", "La empresa seleccionada no existe.");
                return RetornarConErroresDeDiagnostico();
            }

            // ====================================================================
            // PASO 1: LIMPIEZA Y PARSEO MANUAL DE FECHA
            // ====================================================================

            // Ignorar validaciones automáticas
            ModelState.Remove("RegistroCompra.CargaLote");
            ModelState.Remove("RegistroCompra.CargaLoteId");
            ModelState.Remove("RegistroCompra.FechaEmision");

            // Parseo manual de fecha
            if (!string.IsNullOrEmpty(FechaEmisionInput) &&
                DateTime.TryParseExact(FechaEmisionInput, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
            {
                RegistroCompra.FechaEmision = parsedDate;
                RegistroCompra.Anio = (short)parsedDate.Year;
                RegistroCompra.Mes = (short)parsedDate.Month;
                RegistroCompra.FechaRegistro = parsedDate;
            }
            else if (!RegistroCompra.FechaEmision.HasValue)
            {
                ModelState.AddModelError("RegistroCompra.FechaEmision", "El formato de fecha debe ser DD/MM/YYYY.");
            }

            // ====================================================================
            // PASO 2: VERIFICACIÓN DEL MODEL STATE
            // ====================================================================
            if (!ModelState.IsValid)
            {
                return RetornarConErroresDeDiagnostico();
            }

            // ====================================================================
            // PASO 3: LÓGICA DE NEGOCIO Y CUADRATURA
            // ====================================================================

            // Limpieza Comprobante
            RegistroCompra.NumComprobante = RegistroCompra.NumComprobante?.Trim().Replace("-", "") ?? string.Empty;

            // Tipo ID
            string idProv = RegistroCompra.IdProveedor?.Trim() ?? "";
            RegistroCompra.IdProveedor = idProv;
            if (idProv.Length == 13) RegistroCompra.TipoIdProveedor = "01";
            else if (idProv.Length == 10) RegistroCompra.TipoIdProveedor = "02";
            else RegistroCompra.TipoIdProveedor = "03";

            // Sustento
            decimal montoIva = RegistroCompra.MontoIva ?? 0.00M;
            // CodSustento se asume 01 si hay IVA, 02 si no hay, para simplificar el ingreso.
            RegistroCompra.CodSustento = RegistroCompra.CodSustento ?? (montoIva > 0 ? "01" : "02");

            // Cuadratura 🚨 CORRECCIÓN 2: Usar GetValueOrDefault() para todas las operaciones con decimal?
            decimal sumaBases = RegistroCompra.BaseImponible.GetValueOrDefault()
                              + RegistroCompra.BaseImpGrav.GetValueOrDefault()
                              + RegistroCompra.BaseNoGraIva.GetValueOrDefault()
                              + RegistroCompra.BaseImpExe.GetValueOrDefault();
            decimal sumaImpuestos = montoIva + RegistroCompra.MontoIce.GetValueOrDefault();
            decimal totalCalculado = sumaBases + sumaImpuestos;

            if (Math.Abs(RegistroCompra.MontoTotal.GetValueOrDefault() - totalCalculado) > 0.02m)
            {
                ModelState.AddModelError("RegistroCompra.MontoTotal",
                    $"Error de Cuadratura: Total ingresado ${RegistroCompra.MontoTotal:N2} vs Calculado ${totalCalculado:N2}. Ajuste sus bases.");
                return RetornarConErroresDeDiagnostico();
            }

            // Datos de Sistema y Relleno de Campos DB
            RegistroCompra.CodigoCompra = $"C{DateTime.Now.Ticks % 100000}";
            RegistroCompra.ParteRelacionada = false;

            RegistroCompra.BaseNoGraIva = RegistroCompra.BaseNoGraIva ?? 0.00M;
            RegistroCompra.BaseImpExe = RegistroCompra.BaseImpExe ?? 0.00M;
            RegistroCompra.MontoIce = RegistroCompra.MontoIce ?? 0.00M;
            RegistroCompra.MontoIva = montoIva;
            RegistroCompra.BaseImpGrav = RegistroCompra.BaseImpGrav ?? 0.00M;
            RegistroCompra.Autorizacion = RegistroCompra.Autorizacion?.Trim() ?? "9999999999";

            // Campos de retención y otros por defecto para evitar errores NOT NULL en DB
            RegistroCompra.ValRetBien10 = RegistroCompra.ValRetBien10 ?? 0.00M;
            RegistroCompra.ValRetServ20 = RegistroCompra.ValRetServ20 ?? 0.00M;
            RegistroCompra.ValorRetBienes = RegistroCompra.ValorRetBienes ?? 0.00M;
            RegistroCompra.ValRetServ50 = RegistroCompra.ValRetServ50 ?? 0.00M;
            RegistroCompra.ValorRetServicios = RegistroCompra.ValorRetServicios ?? 0.00M;
            RegistroCompra.ValRetServ100 = RegistroCompra.ValRetServ100 ?? 0.00M;
            RegistroCompra.ValorRetencionNc = RegistroCompra.ValorRetencionNc ?? 0.00M;
            RegistroCompra.BaseImpAir = RegistroCompra.BaseImpAir ?? 0.00M;
            RegistroCompra.CodRetAir = RegistroCompra.CodRetAir ?? "332"; // Cod por defecto
            RegistroCompra.PorcentajeAir = RegistroCompra.PorcentajeAir ?? 0.00M;
            RegistroCompra.ValRetAir = RegistroCompra.ValRetAir ?? 0.00M;
            RegistroCompra.PagoLocExt = RegistroCompra.PagoLocExt ?? "01";

            RegistroCompra.UsuarioCreacion = User.Identity?.Name ?? "IngresoManual";
            RegistroCompra.FechaCreacion = DateTime.Now;
            RegistroCompra.CargaLoteId = null;
            RegistroCompra.RucEmpresa = empresa.Ruc;

            // ====================================================================
            // PASO 4: GUARDADO
            // ====================================================================
            try
            {
                _db.Compras.Add(RegistroCompra);
                await _db.SaveChangesAsync();
                TempData["MensajeExito"] = $"✅ Compra guardada correctamente. Documento: {RegistroCompra.NumComprobante}";
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error DB: {ex.Message}");
                if (ex.InnerException != null) ModelState.AddModelError("", $"Detalle: {ex.InnerException.Message}");
                return RetornarConErroresDeDiagnostico();
            }

            return RedirectToPage("./Index");
        }

        private IActionResult RetornarConErroresDeDiagnostico()
        {
            var errors = ModelState.Where(x => x.Value.Errors.Count > 0)
                .Select(x => $"{x.Key}: {string.Join(", ", x.Value.Errors.Select(e => e.ErrorMessage))}")
                .ToList();

            TempData["ValidationErrors"] = string.Join("<br/>", errors);
            return Page();
        }
    }
}