using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using AtsManager.Services;
using AtsManager.Pages.Empresas.Models;

namespace AtsManager.Pages.Retenciones
{
    public class IndexModel : AtsManager.Pages.ReportBasePageModel
    {
        private readonly AtsDbContext _db;
        private const string PdfBasePath = @"C:\descargasSRI";

        public IndexModel(AtsDbContext context, AtsManager.Services.ICurrentCompanyService currentCompany) : base(currentCompany)
        {
            _db = context;
        }
        [TempData]
        public string MensajeProceso { get; set; } = string.Empty;
        public IList<RetencionCliente> Retenciones { get; set; } = new List<RetencionCliente>();

        [BindProperty(SupportsGet = true)]
        public string? SortField { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SortOrder { get; set; }

        public string GetSortIcon(string field) => SortField == field ? (SortOrder == "desc" ? "fas fa-sort-down" : "fas fa-sort-up") : "fas fa-sort";

        private IQueryable<RetencionCliente> ApplySorting(IQueryable<RetencionCliente> query)
        {
            var isDesc = SortOrder == "desc";
            return (SortField) switch
            {
                "Periodo" => isDesc ? query.OrderByDescending(r => r.Anio).ThenByDescending(r => r.Mes) : query.OrderBy(r => r.Anio).ThenBy(r => r.Mes),
                "Fecha" => isDesc ? query.OrderByDescending(r => r.FechaRetencion) : query.OrderBy(r => r.FechaRetencion),
                "Numero" => isDesc ? query.OrderByDescending(r => r.NumRetencion) : query.OrderBy(r => r.NumRetencion),
                "DocAfectado" => isDesc ? query.OrderByDescending(r => r.DocAfectado) : query.OrderBy(r => r.DocAfectado),
                "Cliente" => isDesc ? query.OrderByDescending(r => r.RucEmisor) : query.OrderBy(r => r.RucEmisor),
                "BaseIva" => isDesc ? query.OrderByDescending(r => r.BaseImpGrav) : query.OrderBy(r => r.BaseImpGrav),
                "RetIva" => isDesc ? query.OrderByDescending(r => r.ValRetIva) : query.OrderBy(r => r.ValRetIva),
                "BaseRenta" => isDesc ? query.OrderByDescending(r => r.BaseImpAir) : query.OrderBy(r => r.BaseImpAir),
                "RetRenta" => isDesc ? query.OrderByDescending(r => r.ValRetRenta) : query.OrderBy(r => r.ValRetRenta),
                "Total" => isDesc ? query.OrderByDescending(r => r.TotalRetencion) : query.OrderBy(r => r.TotalRetencion),
                _ => query.OrderByDescending(r => r.FechaRetencion)
            };
        }

        public async Task OnGetAsync()
        {
            await LoadCurrentCompanyAsync();
            string ruc = CurrentRuc;
            var query = _db.RetencionesClientes.AsQueryable();
            if (!string.IsNullOrWhiteSpace(ruc))
            {
                query = query.Where(r => r.RucEmpresa == ruc);
            }
            Retenciones = await ApplySorting(query).ToListAsync();
        }

        public string GetPdfLink(RetencionCliente r)
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

            // RECIBIDOS para retenciones recibidas de clientes
            string carpeta = Path.Combine(@"C:\descargasSRI", r.RucEmpresa, "RECIBIDOS", $"{r.FechaRetencion.Value:yyyy-MM}");
            if (!Directory.Exists(carpeta))
                return string.Empty;

            var archivos = Directory.GetFiles(carpeta, "*.pdf");
            string secuencialLimpio = secuencial.TrimStart('0');

            foreach (var archivo in archivos)
            {
                string nombre = Path.GetFileName(archivo);
                if (nombre.StartsWith("RETENCION-"))
                {
                    if (nombre.Contains("-" + secuencial + "-") || nombre.Contains("-" + secuencialLimpio + "-"))
                        return PdfLinkHelper.GetUrl(archivo);
                }
            }

            return string.Empty;
        }

        public string GetDocAfectadoPdfLink(RetencionCliente r)
        {
            // PDF del documento afectado (factura)
            if (string.IsNullOrEmpty(r.DocAfectado) || !r.FechaDocAfectado.HasValue)
                return string.Empty;

            string secuenciaDoc = r.DocAfectado.Replace("-", "");
            string carpeta = Path.Combine(@"C:\descargasSRI", r.RucEmpresa, "RECIBIDOS", $"{r.FechaDocAfectado.Value:yyyy-MM}");
            if (!Directory.Exists(carpeta))
                return string.Empty;

            var archivos = Directory.GetFiles(carpeta, "*.pdf");

            foreach (var archivo in archivos)
            {
                string nombre = Path.GetFileName(archivo);
                // Buscar por FACTURA-XXX-XXX-XXXXXXXX
                if (nombre.Contains(secuenciaDoc) || nombre.Contains("-" + secuenciaDoc + "-"))
                    return PdfLinkHelper.GetUrl(archivo);
            }

            return string.Empty;
        }
    }
}
