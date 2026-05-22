using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using AtsManager.Services;
using System.Linq.Expressions;
using AtsManager.Pages.Empresas.Models;

namespace AtsManager.Pages.Ventas
{
public class IndexModel : AtsManager.Pages.ReportBasePageModel
{
    private readonly AtsDbContext _db;
        private const string PdfBasePath = @"C:\descargasSRI";

    public IndexModel(AtsDbContext context, AtsManager.Services.ICurrentCompanyService currentCompany) : base(currentCompany)
    {
        _db = context;
    }

        public IList<Venta> Venta { get; set; } = new List<Venta>();

        [TempData]
        public string MensajeProceso { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)]
        public string? Tipo { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SortField { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SortOrder { get; set; }

        public string Titulo { get; set; } = "Ventas";

    public string GetSortIcon(string field) => SortField == field ? (SortOrder == "desc" ? "fas fa-sort-down" : "fas fa-sort-up") : "fas fa-sort";

    private IQueryable<Venta> ApplySorting(IQueryable<Venta> query)
    {
        var isDesc = SortOrder == "desc";
        return (SortField) switch
        {
            "Fecha" => isDesc ? query.OrderByDescending(v => v.FechaEmision).ThenByDescending(v => v.Secuencial) : query.OrderBy(v => v.FechaEmision).ThenByDescending(v => v.Secuencial),
            "Tipo" => isDesc ? query.OrderByDescending(v => v.TipoComprobante) : query.OrderBy(v => v.TipoComprobante),
            "NumComprobante" => isDesc ? query.OrderByDescending(v => v.NumComprobante) : query.OrderBy(v => v.NumComprobante),
            "Cliente" => isDesc ? query.OrderByDescending(v => v.RazonSocialCliente) : query.OrderBy(v => v.RazonSocialCliente),
            "Base0" => isDesc ? query.OrderByDescending(v => v.BaseNoGraIva) : query.OrderBy(v => v.BaseNoGraIva),
            "Base15" => isDesc ? query.OrderByDescending(v => v.BaseImpGrav) : query.OrderBy(v => v.BaseImpGrav),
            "IVA" => isDesc ? query.OrderByDescending(v => v.MontoIva) : query.OrderBy(v => v.MontoIva),
            "Total" => isDesc ? query.OrderByDescending(v => v.MontoTotal) : query.OrderBy(v => v.MontoTotal),
            _ => query.OrderByDescending(v => v.FechaEmision).ThenByDescending(v => v.Secuencial)
        };
    }

    public async Task OnGetAsync()
    {
            Titulo = Tipo switch
            {
                "01" => "Facturas de Ventas",
                "04" => "Notas de Crédito Ventas",
                "05" => "Notas de Débito Ventas",
                "07" => "Retenciones Ventas",
                _ => "Ventas"
            };

            ViewData["Title"] = Titulo;
            
            await LoadCurrentCompanyAsync();
            var rucEmpresa = CurrentRuc;
            
            var query = _db.Ventas.AsQueryable();
            
            if (!string.IsNullOrEmpty(rucEmpresa))
            {
                query = query.Where(v => v.RucEmpresa == rucEmpresa);
            }
            
            if (!string.IsNullOrEmpty(Tipo))
            {
                query = query.Where(v => v.TipoComprobante == Tipo);
            }
            
            Venta = await ApplySorting(query).ToListAsync();
            Venta = Venta ?? new List<Venta>();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int? id)
        {
            if (id == null)
            {
                MensajeProceso = "ERROR: No se especificó un ID para eliminar.";
                return RedirectToPage();
            }

            var venta = await _db.Ventas.FindAsync(id);

            if (venta != null)
            {
                if (venta.CargaLoteId.HasValue)
                {
                    MensajeProceso = "ERROR: No puede eliminar un registro de lote desde esta vista. Use la gestión de lotes.";
                    return RedirectToPage();
                }

                _db.Ventas.Remove(venta);
                await _db.SaveChangesAsync();
                MensajeProceso = $"ÉXITO: La venta {venta.NumComprobante} ha sido eliminada correctamente.";
            }
            else
            {
                MensajeProceso = "ERROR: El registro de venta no fue encontrado.";
            }

            return RedirectToPage();
        }

        public string GetPdfLink(Venta v)
        {
            if (string.IsNullOrEmpty(v.TipoComprobante) || string.IsNullOrEmpty(v.NumComprobante) || !v.FechaEmision.HasValue)
                return string.Empty;

            var tipo = v.TipoComprobante switch
            {
                "01" => "FACTURA",
                "04" => "NOTA_CREDITO",
                "05" => "NOTA_DEBITO",
                "07" => "RETENCION",
                _ => v.TipoComprobante
            };

            // NumComprobante is 15 chars (estab3+ptoEmi3+secuencial9), last 9 = secuencial
            string secuencial = v.Secuencial ?? (v.NumComprobante.Length >= 9 ? v.NumComprobante.Substring(v.NumComprobante.Length - 9) : v.NumComprobante);

            string secuencialLimpio = secuencial.TrimStart('0');
            string mes = $"{v.FechaEmision.Value:yyyy-MM}";

            string[] carpetas = { "EMITIDOS", "RECIBIDOS" };

            foreach (var carpetaBase in carpetas)
            {
                string carpeta = Path.Combine(PdfBasePath, v.RucEmpresa, carpetaBase, mes);
                if (!Directory.Exists(carpeta))
                    continue;

                var archivos = Directory.GetFiles(carpeta, "*.pdf");

                foreach (var archivo in archivos)
                {
                    string nombre = Path.GetFileName(archivo);
                    if (nombre.StartsWith(tipo + "-"))
                    {
                        if (nombre.Contains("-" + secuencial + "-") || nombre.Contains("-" + secuencialLimpio + "-"))
                            return PdfLinkHelper.GetUrl(archivo);
                    }
                }
            }

            return string.Empty;
        }
    }
}
