using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using AtsManager.Pages.Empresas.Models;
using AtsManager.Services;

namespace AtsManager.Pages.Retenciones.Emitidas
{
    public class IndexModel : AtsManager.Pages.ReportBasePageModel
    {
        private readonly AtsDbContext _db;
        private const string PdfBasePath = @"C:\descargasSRI";
        
        public List<RetencionCliente> Retenciones { get; set; } = new List<RetencionCliente>();

        public IndexModel(AtsDbContext context, AtsManager.Services.ICurrentCompanyService currentCompany) : base(currentCompany)
        {
            _db = context;
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
            Retenciones = await query.OrderByDescending(r => r.FechaRetencion).ToListAsync();
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

            string carpeta = System.IO.Path.Combine(PdfBasePath, r.RucEmpresa, "EMITIDOS", $"{r.FechaRetencion.Value:yyyy-MM}");
            if (!Directory.Exists(carpeta))
                return string.Empty;

            var archivos = Directory.GetFiles(carpeta, "*.pdf");
            string secuencialLimpio = secuencial.TrimStart('0');

            foreach (var archivo in archivos)
            {
                string nombre = System.IO.Path.GetFileName(archivo);
                if (nombre.StartsWith("RETENCION_VENTA-") || nombre.StartsWith("RETENCION-"))
                {
                    if (nombre.Contains("-" + secuencial + "-") || nombre.Contains("-" + secuencialLimpio + "-"))
                        return PdfLinkHelper.GetUrl(archivo);
                }
            }

            return string.Empty;
        }
    }
}
