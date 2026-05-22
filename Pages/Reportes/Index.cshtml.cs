using AtsManager.Pages.Empresas.Models;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using AtsManager.Services;

namespace AtsManager.Pages.Reportes
{
    public class IndexModel : PageModel
    {
        private readonly AtsDbContext _db;
        private readonly AtsManager.Services.ICurrentCompanyService _currentCompany;

        public IndexModel(AtsDbContext db, AtsManager.Services.ICurrentCompanyService currentCompany)
        {
            _db = db;
            _currentCompany = currentCompany;
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
        public List<CargaLote> Lotes { get; set; } = new();

        public async Task OnGetAsync()
        {
            Empresas = await _db.Empresas.OrderBy(e => e.RazonSocial).ToListAsync();

            // Cargar empresa actual desde el contexto centralizado
            await _currentCompany.LoadAsync();
            if (string.IsNullOrWhiteSpace(RucEmpresa))
            {
                RucEmpresa = _currentCompany.Ruc;
            }

            // Guardar nombre de empresa para mostrar
            if (!string.IsNullOrWhiteSpace(RucEmpresa))
            {
                var empresa = Empresas.FirstOrDefault(e => e.Ruc == RucEmpresa);
                if (empresa != null)
                {
                    ViewData["EmpresaNombre"] = $"{empresa.Ruc} - {empresa.RazonSocial}";
                    Lotes = await _db.CargasLotes
                        .Where(l => l.Anio == Anio && (Mes == 0 || l.Mes == Mes))
                        .OrderByDescending(l => l.FechaCarga)
                        .ToListAsync();
                    await CargarDatosAsync();
                }
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
            var headers = new[] { "Fecha", "Tipo", "Número", "Doc. Afectado", "RUC", "Nombre", "Base 0%", "Base Gravada", "IVA", "Total", "Retención" };
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
                ws.Cell(row, 4).Value = f.DocAfectado;
                ws.Cell(row, 5).Value = f.RucRelacionado;
                ws.Cell(row, 6).Value = f.NombreRelacionado;
                ws.Cell(row, 7).Value = f.Base0;
                ws.Cell(row, 8).Value = f.BaseGravada;
                ws.Cell(row, 9).Value = f.Iva;
                ws.Cell(row, 10).Value = f.Total;
                ws.Cell(row, 11).Value = f.Retencion;
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
                // Recibidos: Compras (facturas, NC, ND) + Retenciones de clientes (ventas con retención)
                if (string.IsNullOrEmpty(TipoDocumento) || TipoDocumento == "FACTURA" || TipoDocumento == "NOTA_CREDITO" || TipoDocumento == "NOTA_DEBITO")
                {
                    // Cargar compras
                    var queryCompras = _db.Compras
                        .Include(c => c.CargaLote)
                        .Where(c => c.RucEmpresa == RucEmpresa && c.Anio == Anio);

                    if (Mes > 0)
                        queryCompras = queryCompras.Where(c => c.Mes == Mes);

                    if (!string.IsNullOrEmpty(TipoDocumento) && TipoDocumento != "FACTURA")
                    {
                        string codigo = MapTipoDocumento(TipoDocumento);
                        queryCompras = queryCompras.Where(c => c.TipoComprobante == codigo);
                    }

                    var compras = await queryCompras.OrderBy(c => c.FechaEmision).ToListAsync();

                    var filasCompras = compras.Select(c => new ReporteFila
                    {
                        Fecha = c.FechaEmision?.ToString("dd/MM/yyyy") ?? string.Empty,
                        Tipo = TipoDocumentoLegible(c.TipoComprobante),
                        Numero = c.NumComprobante,
                        DocAfectado = !string.IsNullOrEmpty(c.SecuencialModificado) ? $"{c.EstablecimientoModificado}-{c.PuntoEmisionModificado}-{c.SecuencialModificado}" : "",
                        RucRelacionado = c.IdProveedor,
                        NombreRelacionado = c.RazonSocialProveedor,
                        Base0 = c.BaseNoGraIva ?? c.BaseImponible ?? 0,
                        BaseGravada = c.BaseImpGrav ?? 0,
                        Iva = c.MontoIva ?? 0,
                        Total = c.MontoTotal ?? 0,
                        Retencion = (c.ValRetAir ?? 0) + (c.ValorRetServicios ?? 0) + (c.ValRetServ100 ?? 0),
                        LinkPdf = ConstruirLinkPdf(TipoDocumentoLegible(c.TipoComprobante), c.Estab ?? "001", c.PtoEmi ?? "001", c.NumComprobante, c.IdProveedor, c.FechaEmision, Contexto)
                    }).ToList();

                    Filas.AddRange(filasCompras);
                }

                if (string.IsNullOrEmpty(TipoDocumento) || TipoDocumento == "RETENCION")
                {
                    // Cargar retenciones recibidas (tabla RetencionesClientes)
                    var queryRetenciones = _db.RetencionesClientes
                        .Where(r => r.RucEmpresa == RucEmpresa && r.Anio == Anio);

                    if (Mes > 0)
                        queryRetenciones = queryRetenciones.Where(r => r.Mes == Mes);

                    var retencionesRecibidas = await queryRetenciones.OrderBy(r => r.FechaRetencion).ToListAsync();

                    var filasRetenciones = retencionesRecibidas.Select(r => new ReporteFila
                    {
                        Fecha = r.FechaRetencion?.ToString("dd/MM/yyyy") ?? string.Empty,
                        Tipo = "Retención",
                        Numero = r.NumRetencionCompleto,
                        DocAfectado = string.IsNullOrWhiteSpace(r.DocAfectado) ? "0000000000" : r.DocAfectado,
                        RucRelacionado = r.RucEmisor,
                        NombreRelacionado = r.RazonSocialEmisor,
                        Base0 = 0,
                        BaseGravada = r.BaseImpGrav ?? 0,
                        Iva = r.ValRetIva ?? 0,
                        Total = ((r.BaseImpGrav ?? 0) + (r.BaseImpAir ?? 0)),
                        Retencion = (r.ValRetIva ?? 0) + (r.ValRetRenta ?? 0),
                        LinkPdf = ConstruirLinkPdf("RETENCION", r.NumRetencionCompleto?.Split('-').FirstOrDefault() ?? "001", r.NumRetencionCompleto?.Split('-').Skip(1).FirstOrDefault() ?? "001", r.NumRetencion ?? "", r.RucEmisor, r.FechaRetencion, Contexto)
                    }).ToList();

                    Filas.AddRange(filasRetenciones);
                }

                // Ordenar por fecha
                Filas = Filas.OrderBy(f => f.Fecha).ToList();
            }
            else
            {
                // Emitidos: Ventas (facturas, NC, ND) + Retenciones emitidas a clientes (tabla RetencionesClientes)
                if (string.IsNullOrEmpty(TipoDocumento) || TipoDocumento == "FACTURA" || TipoDocumento == "NOTA_CREDITO" || TipoDocumento == "NOTA_DEBITO")
                {
                    // Cargar ventas
                    var queryVentas = _db.Ventas
                        .Include(v => v.CargaLote)
                        .Where(v => v.RucEmpresa == RucEmpresa && v.Anio == Anio);

                    if (Mes > 0)
                        queryVentas = queryVentas.Where(v => v.Mes == Mes);

                    if (!string.IsNullOrEmpty(TipoDocumento) && TipoDocumento != "FACTURA")
                    {
                        string codigo = MapTipoDocumento(TipoDocumento);
                        queryVentas = queryVentas.Where(v => v.TipoComprobante == codigo);
                    }

                    var ventas = await queryVentas.OrderBy(v => v.FechaEmision).ToListAsync();

                    var filasVentas = ventas.Select(v => new ReporteFila
                    {
                        Fecha = v.FechaEmision?.ToString("dd/MM/yyyy") ?? string.Empty,
                        Tipo = TipoDocumentoLegible(v.TipoComprobante),
                        Numero = v.NumComprobante,
                        DocAfectado = "",
                        RucRelacionado = v.IdCliente,
                        NombreRelacionado = v.RazonSocialCliente,
                        Base0 = v.BaseImponible ?? 0,
                        BaseGravada = v.BaseImpGrav ?? 0,
                        Iva = v.MontoIva ?? 0,
                        Total = v.MontoTotal ?? 0,
                        Retencion = (v.valRetIVA ?? 0) + (v.valRetRenta ?? 0),
                        LinkPdf = ConstruirLinkPdf(TipoDocumentoLegible(v.TipoComprobante), v.Estab ?? "001", v.PtoEmi ?? "001", v.NumComprobante, v.IdCliente, v.FechaEmision, Contexto)
                    }).ToList();

                    Filas.AddRange(filasVentas);
                }

                if (string.IsNullOrEmpty(TipoDocumento) || TipoDocumento == "RETENCION" || TipoDocumento == "RETENCION_EMITIDA")
                {
                    // Cargar retenciones emitidas (tabla RetencionesClientes)
                    var queryRetencionesEmitidas = _db.RetencionesClientes
                        .Where(r => r.RucEmpresa == RucEmpresa && r.Anio == Anio);

                    if (Mes > 0)
                        queryRetencionesEmitidas = queryRetencionesEmitidas.Where(r => r.Mes == Mes);

                    var retencionesEmitidas = await queryRetencionesEmitidas.OrderBy(r => r.FechaRetencion).ToListAsync();

                    var filasRetenciones = retencionesEmitidas.Select(r => new ReporteFila
                    {
                        Fecha = r.FechaRetencion?.ToString("dd/MM/yyyy") ?? string.Empty,
                        Tipo = "Retención",
                        Numero = r.NumRetencionCompleto,
                        DocAfectado = string.IsNullOrWhiteSpace(r.DocAfectado) ? "0000000000" : r.DocAfectado,
                        RucRelacionado = r.RucEmisor,
                        NombreRelacionado = r.RazonSocialEmisor,
                        Base0 = 0,
                        BaseGravada = r.BaseImpGrav ?? 0,
                        Iva = r.ValRetIva ?? 0,
                        Total = ((r.BaseImpGrav ?? 0) + (r.BaseImpAir ?? 0)),
                        Retencion = (r.ValRetIva ?? 0) + (r.ValRetRenta ?? 0),
                        LinkPdf = ConstruirLinkPdf("RETENCION", r.NumRetencionCompleto?.Split('-').FirstOrDefault() ?? "001", r.NumRetencionCompleto?.Split('-').Skip(1).FirstOrDefault() ?? "001", r.NumRetencion ?? "", r.RucEmisor, r.FechaRetencion, Contexto)
                    }).ToList();

                    Filas.AddRange(filasRetenciones);
                }

                // Ordenar por fecha
                Filas = Filas.OrderBy(f => f.Fecha).ToList();
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
            public string DocAfectado { get; set; } = string.Empty;
            public string RucRelacionado { get; set; } = string.Empty;
            public string NombreRelacionado { get; set; } = string.Empty;
            public decimal Base0 { get; set; }
            public decimal BaseGravada { get; set; }
            public decimal Iva { get; set; }
            public decimal Total { get; set; }
            public decimal Retencion { get; set; }
            public string LinkPdf { get; set; } = string.Empty;
        }

        private const string PdfBasePath = @"C:\descargasSRI";

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
            else if (numComprobante.Length >= 9)
            {
                secuencial = numComprobante.Substring(numComprobante.Length - 9);
            }

            if (string.IsNullOrEmpty(estabFinal) || estabFinal == "0") estabFinal = "001";
            if (string.IsNullOrEmpty(ptoEmiFinal) || ptoEmiFinal == "0") ptoEmiFinal = "001";

            string secuencialLimpio = secuencial.TrimStart('0');
            string mes = $"{fecha.Value:yyyy-MM}";
            string[] carpetas = contexto == "EMITIDOS" 
                ? new[] { "EMITIDOS", "RECIBIDOS" } 
                : new[] { "RECIBIDOS", "EMITIDOS" };

            foreach (var carpetaBase in carpetas)
            {
                string carpeta = Path.Combine(PdfBasePath, RucEmpresa, carpetaBase, mes);
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
