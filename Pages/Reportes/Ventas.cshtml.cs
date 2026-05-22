using AtsManager.Pages.Empresas.Models;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using System.Linq;  
using System.Threading.Tasks;
using AtsManager.Services;

namespace AtsManager.Pages.Reportes
{
    public class VentasModel : PageModel
    {
        private readonly AtsDbContext _db;

        public List<Venta> Ventas { get; set; } = new();
        public List<RetencionCliente> RetencionesClientes { get; set; } = new();
        public List<Empresa> Empresas { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public int Anio { get; set; } = System.DateTime.Now.Year;

        [BindProperty(SupportsGet = true)]
        public int Mes { get; set; } = System.DateTime.Now.Month;

        [BindProperty(SupportsGet = true)]
        public int EmpresaId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Tipo { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool VerRetenciones { get; set; }

        public decimal TotalBase0 { get; set; }
        public decimal TotalBaseGrav { get; set; }
        public decimal TotalIva { get; set; }
        public decimal TotalTotal { get; set; }

        public decimal TotalBaseIvaRet { get; set; }
        public decimal TotalRetIva { get; set; }
        public decimal TotalBaseRentaRet { get; set; }
        public decimal TotalRetRenta { get; set; }

        private const string PdfBasePath = @"C:\descargasSRI";

        public VentasModel(AtsDbContext db)
        {
            _db = db;
        }

        public async Task OnGetAsync()
        {
            await CargarDatosAsync();
        }

        private async Task CargarDatosAsync()
        {
            var rucEmpresa = HttpContext.Session.GetString("EmpresaSeleccionada") ?? "";
            
            Empresas = await _db.Empresas
                .Where(e => e.Activa)
                .OrderBy(e => e.RazonSocial)
                .ToListAsync();
            
            if (!string.IsNullOrEmpty(rucEmpresa))
            {
                var empresa = await _db.Empresas.FirstOrDefaultAsync(e => e.Ruc == rucEmpresa);
                if (empresa != null)
                {
                    EmpresaId = empresa.Id;
                    ViewData["EmpresaNombre"] = $"{empresa.Ruc} - {empresa.RazonSocial}";
                }
            }
            else if (EmpresaId == 0 && Empresas.Any())
            {
                EmpresaId = Empresas.First().Id;
                ViewData["EmpresaNombre"] = $"{Empresas.First().Ruc} - {Empresas.First().RazonSocial}";
            }

            if (VerRetenciones)
            {
                await CargarRetencionesAsync();
            }
            else
            {
                await CargarVentasAsync();
            }
        }

        private async Task CargarVentasAsync()
        {
            var query = _db.Ventas.AsQueryable();

            if (Anio > 0)
                query = query.Where(v => v.Anio == Anio);

            if (Mes > 0)
            {
                query = query.Where(v => v.Mes == Mes);
            }

            if (EmpresaId > 0)
            {
                var empresa = await _db.Empresas.FindAsync(EmpresaId);
                if (empresa != null)
                {
                    query = query.Where(v => v.RucEmpresa == empresa.Ruc);
                }
            }

            if (!string.IsNullOrEmpty(Tipo))
            {
                query = query.Where(v => v.TipoComprobante == Tipo);
            }

            Ventas = await query
                .OrderBy(v => v.FechaEmision)
                .ThenBy(v => v.NumComprobante)
                .ToListAsync();

            TotalBase0 = Ventas.Sum(v => v.BaseImponible ?? 0);
            TotalBaseGrav = Ventas.Sum(v => v.BaseImpGrav ?? 0);
            TotalIva = Ventas.Sum(v => v.MontoIva ?? 0);
            TotalTotal = Ventas.Sum(v => v.MontoTotal ?? 0);
        }

        private async Task CargarRetencionesAsync()
        {
            var query = _db.RetencionesClientes.AsQueryable();

            query = query.Where(r => r.Anio == Anio);

            if (Mes > 0)
            {
                query = query.Where(r => r.Mes == Mes);
            }

            if (EmpresaId > 0)
            {
                var empresa = await _db.Empresas.FindAsync(EmpresaId);
                if (empresa != null)
                {
                    query = query.Where(r => r.RucEmpresa == empresa.Ruc);
                }
            }

            RetencionesClientes = await query
                .OrderBy(r => r.FechaRetencion)
                .ThenBy(r => r.NumRetencionCompleto)
                .ToListAsync();

            TotalBaseIvaRet = RetencionesClientes.Sum(r => r.BaseImpGrav ?? 0);
            TotalRetIva = RetencionesClientes.Sum(r => r.ValRetIva ?? 0);
            TotalBaseRentaRet = RetencionesClientes.Sum(r => r.BaseImpAir ?? 0);
            TotalRetRenta = RetencionesClientes.Sum(r => r.ValRetRenta ?? 0);
        }

        public async Task<IActionResult> OnPostExportarExcelAsync()
        {
            await CargarDatosAsync();

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Ventas");

            var headers = new[] { "Fecha", "Tipo", "Número", "RUC", "Nombre", "Base 0%", "Base 12%/15%", "IVA", "Total" };
            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(1, i + 1).Value = headers[i];
                worksheet.Cell(1, i + 1).Style.Font.Bold = true;
                worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
            }

            int row = 2;
            foreach (var v in Ventas)
            {
                worksheet.Cell(row, 1).Value = v.FechaEmision?.ToString("dd/MM/yyyy");
                worksheet.Cell(row, 2).Value = GetTipoAbrev(v.TipoComprobante);
                worksheet.Cell(row, 3).Value = v.NumComprobante;
                worksheet.Cell(row, 4).Value = v.IdCliente;
                worksheet.Cell(row, 5).Value = v.RazonSocialCliente;
                worksheet.Cell(row, 6).Value = v.BaseImponible ?? 0;
                worksheet.Cell(row, 7).Value = v.BaseImpGrav ?? 0;
                worksheet.Cell(row, 8).Value = v.MontoIva ?? 0;
                worksheet.Cell(row, 9).Value = v.MontoTotal ?? 0;

                worksheet.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(row, 8).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(row, 9).Style.NumberFormat.Format = "#,##0.00";
                row++;
            }

            row++;
            worksheet.Cell(row, 5).Value = "TOTALES:";
            worksheet.Cell(row, 5).Style.Font.Bold = true;
            worksheet.Cell(row, 6).Value = TotalBase0;
            worksheet.Cell(row, 7).Value = TotalBaseGrav;
            worksheet.Cell(row, 8).Value = TotalIva;
            worksheet.Cell(row, 9).Value = TotalTotal;

            worksheet.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(row, 8).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(row, 9).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(row, 6).Style.Font.Bold = true;
            worksheet.Cell(row, 7).Style.Font.Bold = true;
            worksheet.Cell(row, 8).Style.Font.Bold = true;
            worksheet.Cell(row, 9).Style.Font.Bold = true;

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            var fileName = $"Ventas_{Anio}_{Mes:00}.xlsx";
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        public async Task<IActionResult> OnPostExportarExcelRetencionesAsync()
        {
            await CargarRetencionesAsync();

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Retenciones");

            var headers = new[] { "Período", "Fecha", "No. Retención", "Doc. Afectado", "RUC", "Nombre", "Base IVA", "Ret. IVA", "Base Renta", "% Air", "Ret. Renta", "Total", "Cod." };
            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(1, i + 1).Value = headers[i];
                worksheet.Cell(1, i + 1).Style.Font.Bold = true;
                worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
            }

            int row = 2;
            foreach (var r in RetencionesClientes)
            {
                worksheet.Cell(row, 1).Value = $"{r.Anio}/{r.Mes:00}";
                worksheet.Cell(row, 2).Value = r.FechaRetencion?.ToString("dd/MM/yyyy");
                worksheet.Cell(row, 3).Value = r.NumRetencionCompleto;
                worksheet.Cell(row, 4).Value = string.IsNullOrWhiteSpace(r.DocAfectado) ? "0000000000" : r.DocAfectado;
                worksheet.Cell(row, 5).Value = r.IdCliente;
                worksheet.Cell(row, 6).Value = r.RazonSocialCliente;
                worksheet.Cell(row, 7).Value = r.BaseImpGrav ?? 0;
                worksheet.Cell(row, 8).Value = r.ValRetIva ?? 0;
                worksheet.Cell(row, 9).Value = r.BaseImpAir ?? 0;
                worksheet.Cell(row, 10).Value = r.PorcentajeAir ?? 0;
                worksheet.Cell(row, 11).Value = r.ValRetRenta ?? 0;
                worksheet.Cell(row, 12).Value = ((r.ValRetIva ?? 0) + (r.ValRetRenta ?? 0));
                worksheet.Cell(row, 13).Value = r.CodRetAir;

                worksheet.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(row, 8).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(row, 9).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(row, 10).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(row, 11).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(row, 12).Style.NumberFormat.Format = "#,##0.00";
                row++;
            }

            row++;
            worksheet.Cell(row, 6).Value = "TOTALES:";
            worksheet.Cell(row, 6).Style.Font.Bold = true;
            worksheet.Cell(row, 7).Value = TotalBaseIvaRet;
            worksheet.Cell(row, 8).Value = TotalRetIva;
            worksheet.Cell(row, 9).Value = TotalBaseRentaRet;
            worksheet.Cell(row, 11).Value = TotalRetRenta;
            worksheet.Cell(row, 12).Value = TotalRetIva + TotalRetRenta;

            worksheet.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(row, 8).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(row, 9).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(row, 11).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(row, 12).Style.NumberFormat.Format = "#,##0.00";

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            var fileName = $"RetencionesRecibidas_{Anio}_{Mes:00}.xlsx";
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        public static string GetTipoAbrev(string? tipo)
        {
            return tipo switch
            {
                "01" => "FC",
                "04" => "NC",
                "05" => "ND",
                "07" => "RF",
                "03" => "LIQ",
                "02" => "NV",
                _ => tipo ?? ""
            };
        }

        public string GetPdfLink(Venta v)
        {
            if (string.IsNullOrEmpty(v.TipoComprobante) || string.IsNullOrEmpty(v.NumComprobante) || !v.FechaEmision.HasValue)
                return string.Empty;

            var tipo = GetTipoAbrev(v.TipoComprobante);
            var tipoArchivo = tipo.ToUpper() switch
            {
                "FC" => "FACTURA",
                "NC" => "NOTA_CREDITO",
                "ND" => "NOTA_DEBITO",
                "RF" => "RETENCION",
                _ => tipo.ToUpper()
            };

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
                    if (nombre.StartsWith(tipoArchivo + "-"))
                    {
                        if (nombre.Contains("-" + secuencial + "-") || nombre.Contains("-" + secuencialLimpio + "-"))
                            return PdfLinkHelper.GetUrl(archivo);
                    }
                }
            }

            return string.Empty;
        }

        public string GetPdfLink(RetencionCliente r)
        {
            if (string.IsNullOrEmpty(r.NumRetencion) || !r.FechaRetencion.HasValue)
                return string.Empty;

            string tipoArchivo = "RETENCION";
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
            string[] carpetas = { "RECIBIDOS", "EMITIDOS" };

            foreach (var carpetaBase in carpetas)
            {
                string carpeta = Path.Combine(PdfBasePath, r.RucEmpresa, carpetaBase, mes);
                if (!Directory.Exists(carpeta))
                    continue;

                var archivos = Directory.GetFiles(carpeta, "*.pdf");

                foreach (var archivo in archivos)
                {
                    string nombre = Path.GetFileName(archivo);
                    if (nombre.StartsWith(tipoArchivo + "-"))
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
