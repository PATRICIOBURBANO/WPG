using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using AtsManager.Services;
using AtsManager.Pages.Empresas.Models;

namespace AtsManager.Pages.Compras
{
    public class IndexModel : AtsManager.Pages.ReportBasePageModel
    {
        private readonly AtsDbContext _db;
        private const string PdfBasePath = @"C:\descargasSRI";

        public IndexModel(AtsDbContext context, AtsManager.Services.ICurrentCompanyService currentCompany) : base(currentCompany)
        {
            _db = context;
        }

        public IList<Compra> Compra { get; set; } = default!;

        [TempData]
        public string MensajeProceso { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)]
        public string? Tipo { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SortField { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SortOrder { get; set; }

        public string Titulo { get; set; } = "Compras";

        public string GetSortIcon(string field) => SortField == field ? (SortOrder == "desc" ? "fas fa-sort-down" : "fas fa-sort-up") : "fas fa-sort";

        private IQueryable<Compra> ApplySorting(IQueryable<Compra> query)
        {
            var isDesc = SortOrder == "desc";
            return (SortField) switch
            {
                "Fecha" => isDesc ? query.OrderByDescending(c => c.FechaEmision) : query.OrderBy(c => c.FechaEmision),
                "Tipo" => isDesc ? query.OrderByDescending(c => c.TipoComprobante) : query.OrderBy(c => c.TipoComprobante),
                "NumComprobante" => isDesc ? query.OrderByDescending(c => c.NumComprobante) : query.OrderBy(c => c.NumComprobante),
                "Proveedor" => isDesc ? query.OrderByDescending(c => c.RazonSocialProveedor) : query.OrderBy(c => c.RazonSocialProveedor),
                "Base0" => isDesc ? query.OrderByDescending(c => c.BaseImponible) : query.OrderBy(c => c.BaseImponible),
                "Base15" => isDesc ? query.OrderByDescending(c => c.BaseImpGrav) : query.OrderBy(c => c.BaseImpGrav),
                "IVA" => isDesc ? query.OrderByDescending(c => c.MontoIva) : query.OrderBy(c => c.MontoIva),
                "Total" => isDesc ? query.OrderByDescending(c => c.MontoTotal) : query.OrderBy(c => c.MontoTotal),
                _ => query.OrderByDescending(c => c.FechaEmision)
            };
        }

        public async Task OnGetAsync()
        {
            Titulo = Tipo switch
            {
                "01" => "Facturas de Compras",
                "04" => "Notas de Crédito Compras",
                "05" => "Notas de Débito Compras",
                "07" => "Retenciones Compras",
                _ => "Compras"
            };

            ViewData["Title"] = Titulo;
            
            await LoadCurrentCompanyAsync();
            var rucEmpresa = CurrentRuc;
            
            var query = _db.Compras.AsQueryable();
            
            if (!string.IsNullOrEmpty(rucEmpresa))
            {
                query = query.Where(c => c.RucEmpresa == rucEmpresa);
            }
            
            if (!string.IsNullOrEmpty(Tipo))
            {
                query = query.Where(c => c.TipoComprobante == Tipo);
            }
            
            Compra = await ApplySorting(query).ToListAsync();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int? id)
        {
            if (id == null)
            {
                MensajeProceso = "ERROR: No se especificó un ID para eliminar.";
                return RedirectToPage();
            }

            var compra = await _db.Compras.FindAsync(id);

            if (compra != null)
            {
                if (compra.CargaLoteId.HasValue)
                {
                    MensajeProceso = "ERROR: No puede eliminar un registro de lote desde esta vista. Use la gestión de lotes.";
                    return RedirectToPage();
                }

                _db.Compras.Remove(compra);
                await _db.SaveChangesAsync();
                MensajeProceso = $"ÉXITO: La compra {compra.NumComprobante} ha sido eliminada correctamente.";
            }
            else
            {
                MensajeProceso = "ERROR: El registro de compra no fue encontrado.";
            }

            return RedirectToPage();
        }

        public string GetPdfLink(Compra c)
        {
            if (string.IsNullOrEmpty(c.TipoComprobante) || string.IsNullOrEmpty(c.NumComprobante) || !c.FechaEmision.HasValue)
                return string.Empty;

            var tipo = c.TipoComprobante switch
            {
                "01" => "FACTURA",
                "04" => "NOTA_CREDITO",
                "05" => "NOTA_DEBITO",
                "07" => "RETENCION",
                _ => c.TipoComprobante
            };

            string secuencial = c.NumComprobante;

            if (c.NumComprobante.Contains("-"))
            {
                var partes = c.NumComprobante.Split('-');
                if (partes.Length >= 3)
                {
                    secuencial = partes[2];
                }
            }

            string secuencialLimpio = secuencial.TrimStart('0');
            string mes = $"{c.FechaEmision.Value:yyyy-MM}";

            string[] carpetas = { "RECIBIDOS", "EMITIDOS" };

            foreach (var carpetaBase in carpetas)
            {
                string carpeta = Path.Combine(PdfBasePath, c.RucEmpresa, carpetaBase, mes);
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
