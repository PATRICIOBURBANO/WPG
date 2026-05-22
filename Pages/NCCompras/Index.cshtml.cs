using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using AtsManager.Pages.Empresas.Models;
using AtsManager.Services;
using AtsManager.Models;

namespace AtsManager.Pages.NCCompras
{
    public class IndexModel : AtsManager.Pages.ReportBasePageModel
    {
        private readonly AtsDbContext _db;
        private const string PdfBasePath = @"C:\descargasSRI";

        public IndexModel(AtsDbContext context, AtsManager.Services.ICurrentCompanyService currentCompany) : base(currentCompany)
        {
            _db = context;
        }

        public IList<NCCompra> NotasCredito { get; set; } = new List<NCCompra>();

        [TempData]
        public string MensajeProceso { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)]
        public string? SortField { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SortOrder { get; set; }

        public string GetSortIcon(string field) => SortField == field ? (SortOrder == "desc" ? "fas fa-sort-down" : "fas fa-sort-up") : "fas fa-sort";

        public async Task OnGetAsync()
        {
            ViewData["Title"] = "Notas de Crédito Compras";
            await LoadCurrentCompanyAsync();
            var ruc = CurrentRuc;
            
            IQueryable<Compra> query = _db.Compras.Where(c => c.TipoComprobante == "04");
            if (!string.IsNullOrEmpty(ruc))
                query = query.Where(c => c.RucEmpresa == ruc);

            var list = await query
                .Select(c => new NCCompra
                {
                    Id = c.Id,
                    ClaveAcceso = c.Autorizacion ?? "",
                    FechaEmision = c.FechaEmision ?? DateTime.Now,
                    FechaAutorizacion = c.FechaRegistro ?? DateTime.Now,
                    SerieComprobante = c.NumComprobante ?? "",
                    NumeroDocumentoModificado = (c.EstablecimientoModificado ?? "") + "-" + (c.PuntoEmisionModificado ?? "") + "-" + (c.SecuencialModificado ?? ""),
                    RucEmisor = c.IdProveedor ?? "",
                    RazonSocialEmisor = c.RazonSocialProveedor ?? "",
                    ValorSinImpuestos = (c.BaseImponible ?? 0) + (c.BaseNoGraIva ?? 0),
                    IVA = c.MontoIva ?? 0,
                    Total = c.MontoTotal ?? 0,
                    Anio = c.Anio ?? (short)(DateTime.Now.Year),
                    Mes = c.Mes ?? (short)(DateTime.Now.Month),
                    FechaRegistro = c.FechaRegistro ?? DateTime.Now,
                    UsuarioCreacion = c.UsuarioCreacion ?? "",
                    FechaCreacion = c.FechaCreacion
                })
                .ToListAsync();

            var isDesc = SortOrder == "desc";
            NotasCredito = (SortField) switch
            {
                "Periodo" => isDesc ? list.OrderByDescending(n => n.Anio).ThenByDescending(n => n.Mes).ToList() : list.OrderBy(n => n.Anio).ThenBy(n => n.Mes).ToList(),
                "Fecha" => isDesc ? list.OrderByDescending(n => n.FechaEmision).ToList() : list.OrderBy(n => n.FechaEmision).ToList(),
                "Numero" => isDesc ? list.OrderByDescending(n => n.SerieComprobante).ToList() : list.OrderBy(n => n.SerieComprobante).ToList(),
                "DocModificado" => isDesc ? list.OrderByDescending(n => n.NumeroDocumentoModificado).ToList() : list.OrderBy(n => n.NumeroDocumentoModificado).ToList(),
                "Proveedor" => isDesc ? list.OrderByDescending(n => n.RazonSocialEmisor).ToList() : list.OrderBy(n => n.RazonSocialEmisor).ToList(),
                "BaseGrav" => isDesc ? list.OrderByDescending(n => n.ValorSinImpuestos).ToList() : list.OrderBy(n => n.ValorSinImpuestos).ToList(),
                "IVA" => isDesc ? list.OrderByDescending(n => n.IVA).ToList() : list.OrderBy(n => n.IVA).ToList(),
                "Total" => isDesc ? list.OrderByDescending(n => n.Total).ToList() : list.OrderBy(n => n.Total).ToList(),
                _ => list.OrderByDescending(n => n.FechaEmision).ToList()
            };
        }

        public string GetPdfLink(NCCompra nc)
        {
            if (string.IsNullOrEmpty(nc.SerieComprobante) || !nc.FechaEmision.Equals(DateTime.MinValue))
                return string.Empty;

            string secuencial = nc.SerieComprobante;
            if (nc.SerieComprobante.Contains("-"))
            {
                var partes = nc.SerieComprobante.Split('-');
                if (partes.Length >= 3)
                {
                    secuencial = partes[2];
                }
            }

            string carpeta = Path.Combine(PdfBasePath, "compras", $"{nc.FechaEmision:yyyy-MM}");
            if (!Directory.Exists(carpeta))
                return string.Empty;

            var archivos = Directory.GetFiles(carpeta, "*.pdf");
            string secuencialLimpio = secuencial.TrimStart('0');

            foreach (var archivo in archivos)
            {
                string nombre = Path.GetFileName(archivo);
                if (nombre.StartsWith("NOTA_CREDITO-"))
                {
                    if (nombre.Contains("-" + secuencial + "-") || nombre.Contains("-" + secuencialLimpio + "-"))
                        return PdfLinkHelper.GetUrl(archivo);
                }
            }

            return string.Empty;
        }
    }
}
