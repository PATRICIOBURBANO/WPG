using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using AtsManager.Pages.Empresas.Models;
using AtsManager.Services;

namespace AtsManager.Pages.RetencionesCompras
{
    public class IndexModel : PageModel
    {
        private readonly AtsDbContext _db;
        private const string PdfBasePath = @"C:\descargasSRI";

        public IndexModel(AtsDbContext context)
        {
            _db = context;
        }

        [TempData]
        public string MensajeProceso { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)]
        public string? SortField { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SortOrder { get; set; }

        public string GetSortIcon(string field) => SortField == field ? (SortOrder == "desc" ? "fas fa-sort-down" : "fas fa-sort-up") : "fas fa-sort";

        public IList<RetencionCompra> Retenciones { get; set; } = new List<RetencionCompra>();

        public async Task OnGetAsync()
        {
            var query = _db.RetencionesCompras.AsQueryable();
            var isDesc = SortOrder == "desc";
            query = (SortField) switch
            {
                "Periodo" => isDesc ? query.OrderByDescending(r => r.Anio).ThenByDescending(r => r.Mes) : query.OrderBy(r => r.Anio).ThenBy(r => r.Mes),
                "Fecha" => isDesc ? query.OrderByDescending(r => r.FechaRetencion) : query.OrderBy(r => r.FechaRetencion),
                "Numero" => isDesc ? query.OrderByDescending(r => r.NumRetencionCompleto) : query.OrderBy(r => r.NumRetencionCompleto),
                "DocAfectado" => isDesc ? query.OrderByDescending(r => r.DocAfectado) : query.OrderBy(r => r.DocAfectado),
                "Proveedor" => isDesc ? query.OrderByDescending(r => r.RazonSocialProveedor) : query.OrderBy(r => r.RazonSocialProveedor),
                "BaseIva" => isDesc ? query.OrderByDescending(r => r.BaseImpGrav) : query.OrderBy(r => r.BaseImpGrav),
                "RetIva" => isDesc ? query.OrderByDescending(r => r.ValRetIva) : query.OrderBy(r => r.ValRetIva),
                "BaseRenta" => isDesc ? query.OrderByDescending(r => r.BaseImpAir) : query.OrderBy(r => r.BaseImpAir),
                "PorcAir" => isDesc ? query.OrderByDescending(r => r.PorcentajeAir) : query.OrderBy(r => r.PorcentajeAir),
                "RetRenta" => isDesc ? query.OrderByDescending(r => r.ValRetRenta) : query.OrderBy(r => r.ValRetRenta),
                "Total" => isDesc ? query.OrderByDescending(r => r.TotalRetencion) : query.OrderBy(r => r.TotalRetencion),
                _ => query.OrderByDescending(r => r.FechaRetencion)
            };
            Retenciones = await query.ToListAsync();
        }

        public string GetPdfLink(RetencionCompra r)
        {
            if (string.IsNullOrEmpty(r.NumRetencion) || !r.FechaRetencion.HasValue)
                return string.Empty;

            string secuencial = r.NumRetencion;
            if (r.NumRetencion.Contains("-"))
            {
                var partes = r.NumRetencion.Split('-');
                if (partes.Length >= 3)
                    secuencial = partes[2];
            }
            else if (r.NumRetencion.Length >= 9)
            {
                secuencial = r.NumRetencion.Substring(r.NumRetencion.Length - 9);
            }

            string secuencialLimpio = secuencial.TrimStart('0');
            string mes = $"{r.FechaRetencion.Value:yyyy-MM}";
            // Try RECIBIDOS\{RUC}\{mes}, compras\{RUC}\{mes}, and compras\{mes}
            string[][] rutas = {
                new[] { PdfBasePath, r.RucEmpresa, "RECIBIDOS", mes },
                new[] { PdfBasePath, r.RucEmpresa, "compras", mes },
                new[] { PdfBasePath, "compras", mes }
            };

            foreach (var partes in rutas)
            {
                string carpeta = Path.Combine(partes);
                if (!Directory.Exists(carpeta))
                    continue;

                var archivos = Directory.GetFiles(carpeta, "*.pdf");

                foreach (var archivo in archivos)
                {
                    string nombre = Path.GetFileName(archivo);
                    if (nombre.StartsWith("RETENCION_VENTA-") || nombre.StartsWith("RETENCION-"))
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
