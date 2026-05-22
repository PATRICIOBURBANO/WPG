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
    public class ComprasModel : PageModel
    {
        private readonly AtsDbContext _db;

        public List<Compra> Compras { get; set; } = new();
        public List<Empresa> Empresas { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public int Anio { get; set; } = System.DateTime.Now.Year;

        [BindProperty(SupportsGet = true)]
        public int Mes { get; set; } = System.DateTime.Now.Month;

        [BindProperty(SupportsGet = true)]
        public int EmpresaId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Tipo { get; set; }

        public decimal TotalBase0 { get; set; }
        public decimal TotalBaseGrav { get; set; }
        public decimal TotalIva { get; set; }
        public decimal TotalTotal { get; set; }

        private const string PdfBasePath = @"C:\descargasSRI";

        public ComprasModel(AtsDbContext db)
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

            var query = _db.Compras.AsQueryable();

            if (Anio > 0)
                query = query.Where(c => c.Anio == Anio);

            if (Mes > 0)
            {
                query = query.Where(c => c.Mes == Mes);
            }

            if (EmpresaId > 0)
            {
                var empresa = await _db.Empresas.FindAsync(EmpresaId);
                if (empresa != null)
                {
                    query = query.Where(c => c.RucEmpresa == empresa.Ruc);
                }
            }

            if (!string.IsNullOrEmpty(Tipo))
            {
                query = query.Where(c => c.TipoComprobante == Tipo);
            }

            Compras = await query
                .OrderBy(c => c.FechaEmision)
                .ThenBy(c => c.NumComprobante)
                .ToListAsync();

            TotalBase0 = Compras.Sum(c => c.BaseImponible ?? 0);
            TotalBaseGrav = Compras.Sum(c => c.BaseImpGrav ?? 0);
            TotalIva = Compras.Sum(c => c.MontoIva ?? 0);
            TotalTotal = Compras.Sum(c => c.MontoTotal ?? 0);
        }

        public async Task<IActionResult> OnPostExportarExcelAsync()
        {
            await CargarDatosAsync();

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Compras");

            var headers = new[] { "Fecha", "Tipo", "Número", "RUC", "Nombre", "Base 0%", "Base 12%/15%", "IVA", "Total" };
            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(1, i + 1).Value = headers[i];
                worksheet.Cell(1, i + 1).Style.Font.Bold = true;
                worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
            }

            int row = 2;
            foreach (var c in Compras)
            {
                worksheet.Cell(row, 1).Value = c.FechaEmision?.ToString("dd/MM/yyyy");
                worksheet.Cell(row, 2).Value = GetTipoAbrev(c.TipoComprobante);
                worksheet.Cell(row, 3).Value = c.NumComprobante;
                worksheet.Cell(row, 4).Value = c.IdProveedor;
                worksheet.Cell(row, 5).Value = c.RazonSocialProveedor;
                worksheet.Cell(row, 6).Value = c.BaseImponible ?? 0;
                worksheet.Cell(row, 7).Value = c.BaseImpGrav ?? 0;
                worksheet.Cell(row, 8).Value = c.MontoIva ?? 0;
                worksheet.Cell(row, 9).Value = c.MontoTotal ?? 0;

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

            var fileName = $"Compras_{Anio}_{Mes:00}.xlsx";
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

        public string GetPdfLink(Compra c)
        {
            if (string.IsNullOrEmpty(c.TipoComprobante) || string.IsNullOrEmpty(c.NumComprobante) || !c.FechaEmision.HasValue)
                return string.Empty;

            var tipo = GetTipoAbrev(c.TipoComprobante);
            var tipoArchivo = tipo.ToUpper() switch
            {
                "FC" => "FACTURA",
                "NC" => "NOTA_CREDITO",
                "ND" => "NOTA_DEBITO",
                "RF" => "RETENCION",
                _ => tipo.ToUpper()
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
