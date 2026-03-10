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
    public class IndexModel : PageModel
    {
        private readonly AtsDbContext _db;

        public IndexModel(AtsDbContext db)
        {
            _db = db;
        }

        [BindProperty(SupportsGet = true)] public string RucEmpresa { get; set; } = string.Empty;
        [BindProperty(SupportsGet = true)] public int Anio { get; set; } = System.DateTime.Now.Year;
        [BindProperty(SupportsGet = true)] public int Mes { get; set; } = System.DateTime.Now.Month;
        [BindProperty(SupportsGet = true)] public string Contexto { get; set; } = "RECIBIDOS";
        [BindProperty(SupportsGet = true)] public string TipoDocumento { get; set; } = "FACTURA";

        public string Mensaje { get; set; } = string.Empty;
        public bool MostrarResultados { get; set; }
        public List<ReporteFila> Filas { get; set; } = new();
        public decimal TotalBase0 { get; set; }
        public decimal TotalBaseGrav { get; set; }
        public decimal TotalIva { get; set; }
        public decimal TotalTotal { get; set; }
        public List<Empresa> Empresas { get; set; } = new();

        public async Task OnGetAsync()
        {
            Empresas = await _db.Empresas.OrderBy(e => e.RazonSocial).ToListAsync();
            
            if (!string.IsNullOrWhiteSpace(RucEmpresa))
            {
                await CargarDatosAsync();
            }
        }

        public async Task<IActionResult> OnPostExportarExcelAsync()
        {
            await CargarDatosAsync();
            if (!Filas.Any())
            {
                Mensaje = string.IsNullOrWhiteSpace(Mensaje) ? "No hay datos para exportar." : Mensaje;
                return Page();
            }

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Reporte");
            var headers = new[] { "Fecha", "Tipo", "Número", "RUC", "Nombre", "Base 0%", "Base Gravada", "IVA", "Total", "Retención" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
                ws.Cell(1, i + 1).Style.Font.Bold = true;
                ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
            }

            int row = 2;
            foreach (var f in Filas)
            {
                ws.Cell(row, 1).Value = f.Fecha;
                ws.Cell(row, 2).Value = f.Tipo;
                ws.Cell(row, 3).Value = f.Numero;
                ws.Cell(row, 4).Value = f.RucRelacionado;
                ws.Cell(row, 5).Value = f.NombreRelacionado;
                ws.Cell(row, 6).Value = f.Base0;
                ws.Cell(row, 7).Value = f.BaseGravada;
                ws.Cell(row, 8).Value = f.Iva;
                ws.Cell(row, 9).Value = f.Total;
                ws.Cell(row, 10).Value = f.Retencion;
                row++;
            }

            ws.Columns().AdjustToContents();
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Reporte_{Contexto}_{TipoDocumento}_{RucEmpresa}_{Anio}_{Mes:00}.xlsx");
        }

        public IActionResult OnGetVerPdf(string ruta)
        {
            if (string.IsNullOrEmpty(ruta) || !System.IO.File.Exists(ruta))
                return NotFound("Archivo no encontrado");

            var fileBytes = System.IO.File.ReadAllBytes(ruta);
            return File(fileBytes, "application/pdf");
        }

        private async Task CargarDatosAsync()
        {
            Filas = new List<ReporteFila>();
            MostrarResultados = true;
            RucEmpresa = (RucEmpresa ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(RucEmpresa))
            {
                Mensaje = "Debe ingresar el RUC de la empresa para consultar el reporte.";
                return;
            }

            if (Contexto == "RECIBIDOS")
            {
                if (TipoDocumento == "RETENCION")
                {
                    var ventas = await _db.Ventas
                        .Include(v => v.CargaLote)
                        .Where(v => v.RucEmpresa == RucEmpresa && v.Anio == Anio && v.Mes == Mes)
                        .OrderBy(v => v.FechaRetencion ?? v.FechaEmision)
                        .ToListAsync();

                    Filas = ventas.Select(v => new ReporteFila
                    {
                        Fecha = (v.FechaRetencion ?? v.FechaEmision)?.ToString("dd/MM/yyyy") ?? string.Empty,
                        Tipo = "Retención",
                        Numero = v.NumRetencion ?? v.NumComprobante,
                        RucRelacionado = v.IdCliente,
                        NombreRelacionado = v.RazonSocialCliente,
                        Base0 = v.BaseImponible ?? 0,
                        BaseGravada = v.BaseImpGrav ?? 0,
                        Iva = v.MontoIva ?? 0,
                        Total = v.MontoTotal ?? 0,
                        Retencion = (v.valRetIVA ?? 0) + (v.valRetRenta ?? 0),
                        LinkPdf = ConstruirLinkPdf("RETENCION", v.Estab ?? "001", v.PtoEmi ?? "001", v.NumRetencion ?? v.NumComprobante, v.IdCliente, v.FechaRetencion ?? v.FechaEmision, Contexto)
                    }).ToList();
                }
                else
                {
                    string codigo = MapTipoDocumento(TipoDocumento);
                    var compras = await _db.Compras
                        .Include(c => c.CargaLote)
                        .Where(c => c.RucEmpresa == RucEmpresa && c.Anio == Anio && c.Mes == Mes && c.TipoComprobante == codigo)
                        .OrderBy(c => c.FechaEmision)
                        .ToListAsync();

                    Filas = compras.Select(c => new ReporteFila
                    {
                        Fecha = c.FechaEmision?.ToString("dd/MM/yyyy") ?? string.Empty,
                        Tipo = TipoDocumentoLegible(c.TipoComprobante),
                        Numero = c.NumComprobante,
                        RucRelacionado = c.IdProveedor,
                        NombreRelacionado = c.RazonSocialProveedor,
                        Base0 = c.BaseNoGraIva ?? c.BaseImponible ?? 0,
                        BaseGravada = c.BaseImpGrav ?? 0,
                        Iva = c.MontoIva ?? 0,
                        Total = c.MontoTotal ?? 0,
                        Retencion = (c.ValRetAir ?? 0) + (c.ValorRetServicios ?? 0) + (c.ValRetServ100 ?? 0),
                        LinkPdf = ConstruirLinkPdf(TipoDocumentoLegible(c.TipoComprobante), c.Estab ?? "001", c.PtoEmi ?? "001", c.NumComprobante, c.IdProveedor, c.FechaEmision, Contexto)
                    }).ToList();
                }
            }
            else
            {
                if (TipoDocumento == "RETENCION")
                {
                    var compras = await _db.Compras
                        .Include(c => c.CargaLote)
                        .Where(c => c.RucEmpresa == RucEmpresa && c.Anio == Anio && c.Mes == Mes && c.TipoComprobante == "07")
                        .OrderBy(c => c.FechaEmision)
                        .ToListAsync();

                    Filas = compras.Select(c => new ReporteFila
                    {
                        Fecha = c.FechaEmision?.ToString("dd/MM/yyyy") ?? string.Empty,
                        Tipo = "Retención",
                        Numero = c.NumComprobante,
                        RucRelacionado = c.IdProveedor,
                        NombreRelacionado = c.RazonSocialProveedor,
                        Base0 = c.BaseNoGraIva ?? c.BaseImponible ?? 0,
                        BaseGravada = c.BaseImpGrav ?? 0,
                        Iva = c.MontoIva ?? 0,
                        Total = c.MontoTotal ?? 0,
                        Retencion = (c.ValRetAir ?? 0) + (c.ValorRetServicios ?? 0) + (c.ValRetServ100 ?? 0),
                        LinkPdf = ConstruirLinkPdf("RETENCION", c.Estab ?? "001", c.PtoEmi ?? "001", c.NumComprobante, c.IdProveedor, c.FechaEmision, Contexto)
                    }).ToList();
                }
                else
                {
                    string codigo = MapTipoDocumento(TipoDocumento);
                    var ventas = await _db.Ventas
                        .Include(v => v.CargaLote)
                        .Where(v => v.RucEmpresa == RucEmpresa && v.Anio == Anio && v.Mes == Mes && v.TipoComprobante == codigo)
                        .OrderBy(v => v.FechaEmision)
                        .ToListAsync();

                    Filas = ventas.Select(v => new ReporteFila
                    {
                        Fecha = v.FechaEmision?.ToString("dd/MM/yyyy") ?? string.Empty,
                        Tipo = TipoDocumentoLegible(v.TipoComprobante),
                        Numero = v.NumComprobante,
                        RucRelacionado = v.IdCliente,
                        NombreRelacionado = v.RazonSocialCliente,
                        Base0 = v.BaseImponible ?? 0,
                        BaseGravada = v.BaseImpGrav ?? 0,
                        Iva = v.MontoIva ?? 0,
                        Total = v.MontoTotal ?? 0,
                        Retencion = (v.valRetIVA ?? 0) + (v.valRetRenta ?? 0),
                        LinkPdf = ConstruirLinkPdf(TipoDocumentoLegible(v.TipoComprobante), v.Estab ?? "001", v.PtoEmi ?? "001", v.NumComprobante, v.IdCliente, v.FechaEmision, Contexto)
                    }).ToList();
                }
            }

            TotalBase0 = Filas.Sum(f => f.Base0);
            TotalBaseGrav = Filas.Sum(f => f.BaseGravada);
            TotalIva = Filas.Sum(f => f.Iva);
            TotalTotal = Filas.Sum(f => f.Total);

            if (!Filas.Any())
            {
                Mensaje = "No se encontraron registros para los filtros seleccionados.";
            }
        }

        public static string MapTipoDocumento(string tipo) => tipo switch
        {
            "FACTURA" => "01",
            "NOTA_CREDITO" => "04",
            "NOTA_DEBITO" => "05",
            _ => tipo
        };

        public static string TipoDocumentoLegible(string? tipo) => tipo switch
        {
            "01" => "Factura",
            "04" => "Nota Crédito",
            "05" => "Nota Débito",
            "07" => "Retención",
            _ => tipo ?? string.Empty
        };

        public class ReporteFila
        {
            public string Fecha { get; set; } = string.Empty;
            public string Tipo { get; set; } = string.Empty;
            public string Numero { get; set; } = string.Empty;
            public string RucRelacionado { get; set; } = string.Empty;
            public string NombreRelacionado { get; set; } = string.Empty;
            public decimal Base0 { get; set; }
            public decimal BaseGravada { get; set; }
            public decimal Iva { get; set; }
            public decimal Total { get; set; }
            public decimal Retencion { get; set; }
            public string LinkPdf { get; set; } = string.Empty;
        }

        private const string PdfBasePath = @"C:\Users\patri\Downloads\SRI\salida_sri";

        private string ConstruirLinkPdf(string tipo, string estab, string ptoEmi, string numComprobante, string rucRelacionado, DateTime? fecha, string contexto)
        {
            if (string.IsNullOrEmpty(tipo) || string.IsNullOrEmpty(numComprobante) || !fecha.HasValue)
                return string.Empty;

            var tipoArchivo = tipo.ToUpper() switch
            {
                "FACTURA" => "FACTURA",
                "NOTA_CREDITO" or "NOTA CRÉDITO" or "04" => "NOTA_CREDITO",
                "NOTA_DEBITO" or "NOTA DÉBITO" or "05" => "NOTA_DEBITO",
                "RETENCIÓN" or "RETENCION" or "07" => "RETENCION",
                _ => tipo.ToUpper()
            };

            string estabFinal = estab ?? "";
            string ptoEmiFinal = ptoEmi ?? "";
            string secuencial = numComprobante;

            if (numComprobante.Contains("-"))
            {
                var partes = numComprobante.Split('-');
                if (partes.Length >= 3)
                {
                    if (string.IsNullOrEmpty(estabFinal)) estabFinal = partes[0];
                    if (string.IsNullOrEmpty(ptoEmiFinal)) ptoEmiFinal = partes[1];
                    secuencial = partes[2];
                }
            }

            if (string.IsNullOrEmpty(estabFinal) || estabFinal == "0") estabFinal = "001";
            if (string.IsNullOrEmpty(ptoEmiFinal) || ptoEmiFinal == "0") ptoEmiFinal = "001";

            string carpeta = Path.Combine(PdfBasePath, $"RUC_{RucEmpresa}", contexto, $"{fecha.Value:yyyy-MM}");
            if (!Directory.Exists(carpeta))
                return string.Empty;

            var archivos = Directory.GetFiles(carpeta, "*.pdf");
            
            string secuencialLimpio = secuencial.TrimStart('0');
            
            foreach (var archivo in archivos)
            {
                string nombre = Path.GetFileName(archivo);
                if (nombre.StartsWith(tipoArchivo + "-"))
                {
                    if (nombre.Contains("-" + secuencial + "-") || nombre.Contains("-" + secuencialLimpio + "-"))
                        return archivo;
                }
            }

            return string.Empty;
        }
    }
}
