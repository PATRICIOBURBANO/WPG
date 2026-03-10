using AtsManager.Models;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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
            Empresas = await _db.Empresas
                .Where(e => e.Activa)
                .OrderBy(e => e.RazonSocial)
                .ToListAsync();

            var query = _db.Compras.AsQueryable();

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
    }
}
