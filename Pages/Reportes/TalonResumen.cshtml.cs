using AtsManager.Pages.Empresas.Models;
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
    public class TalonResumenModel : PageModel
    {
        private readonly AtsDbContext _db;
        private readonly AtsManager.Services.ICurrentCompanyService _currentCompany;
        public TalonResumenModel(AtsDbContext db, AtsManager.Services.ICurrentCompanyService currentCompany)
        {
            _db = db;
            _currentCompany = currentCompany;
        }

        [BindProperty(SupportsGet = true)] public string RucEmpresa { get; set; } = string.Empty;
        [BindProperty(SupportsGet = true)] public int Anio { get; set; } = System.DateTime.Now.Year;
        [BindProperty(SupportsGet = true)] public int Mes { get; set; } = System.DateTime.Now.Month;

        public bool TieneDatos { get; set; }
        public List<ResumenFila> ComprasResumen { get; set; } = new();
        public List<ResumenFila> VentasResumen { get; set; } = new();
        public List<Empresa> Empresas { get; set; } = new();

        public decimal TotalComprasBase0 { get; set; }
        public decimal TotalComprasBaseGrav { get; set; }
        public decimal TotalComprasBaseNoGraIva { get; set; }
        public decimal TotalComprasIva { get; set; }

        public decimal TotalVentasBase0 { get; set; }
        public decimal TotalVentasBaseGrav { get; set; }
        public decimal TotalVentasBaseNoGraIva { get; set; }
        public decimal TotalVentasIva { get; set; }

        public decimal TotalRetIva10 { get; set; }
        public decimal TotalRetIva20 { get; set; }
        public decimal TotalRetIva30 { get; set; }
        public decimal TotalRetIva50 { get; set; }
        public decimal TotalRetIva70 { get; set; }
        public decimal TotalRetIva100 { get; set; }
        public decimal TotalRetIvaRecibida { get; set; }
        public decimal TotalRetIva => TotalRetIva10 + TotalRetIva20 + TotalRetIva30 + TotalRetIva50 + TotalRetIva70 + TotalRetIva100;

        public decimal TotalRetRentaRecibida { get; set; }
        public decimal TotalRetRentaEmitida { get; set; }
        public decimal TotalRetRenta => TotalRetRentaRecibida + TotalRetRentaEmitida;

        public async Task OnGetAsync()
        {
            Empresas = await _db.Empresas.OrderBy(e => e.RazonSocial).ToListAsync();

            // Auto-select company from session if not provided
            await _currentCompany.LoadAsync();
            if (string.IsNullOrWhiteSpace(RucEmpresa))
            {
                RucEmpresa = _currentCompany.Ruc;
            }

            if (!string.IsNullOrWhiteSpace(RucEmpresa))
            {
                await CargarResumenAsync();
            }
        }

        private async Task CargarResumenAsync()
        {
            var comprasQuery = _db.Compras.Where(c => c.RucEmpresa == RucEmpresa);
            if (Anio > 0)
                comprasQuery = comprasQuery.Where(c => c.Anio == Anio);
            if (Mes > 0)
                comprasQuery = comprasQuery.Where(c => c.Mes == Mes);

            var ventasQuery = _db.Ventas.Where(v => v.RucEmpresa == RucEmpresa);
            if (Anio > 0)
                ventasQuery = ventasQuery.Where(v => v.Anio == Anio);
            if (Mes > 0)
                ventasQuery = ventasQuery.Where(v => v.Mes == Mes);

            var compras = await comprasQuery.ToListAsync();
            var ventas = await ventasQuery.ToListAsync();

            TieneDatos = compras.Any() || ventas.Any();

            var comprasAgrupadas = compras
                .GroupBy(c => c.TipoComprobante)
                .Select(g => new
                {
                    Codigo = g.Key,
                    Count = g.Count(),
                    Base0 = g.Sum(x => (x.BaseNoGraIva ?? 0) + (x.BaseImponible ?? 0)),
                    BaseGrav = g.Sum(x => x.BaseImpGrav ?? 0),
                    BaseNoGraIva = g.Sum(x => x.BaseNoGraIva ?? 0),
                    MontoIva = g.Sum(x => x.MontoIva ?? 0)
                })
                .ToDictionary(x => x.Codigo ?? "", x => x);

            var ventasAgrupadas = ventas
                .GroupBy(v => v.TipoComprobante)
                .Select(g => new
                {
                    Codigo = g.Key,
                    Count = g.Count(),
                    Base0 = g.Sum(x => (x.BaseNoGraIva ?? 0) + (x.BaseImponible ?? 0)),
                    BaseGrav = g.Sum(x => x.BaseImpGrav ?? 0),
                    BaseNoGraIva = g.Sum(x => x.BaseNoGraIva ?? 0),
                    MontoIva = g.Sum(x => x.MontoIva ?? 0)
                })
                .ToDictionary(x => x.Codigo ?? "", x => x);

            var comprasTipos = new Dictionary<string, (string Transaccion, bool Negativo)>
            {
                { "01", ("Factura", false) },
                { "02", ("Nota o boleta de venta", false) },
                { "03", ("Liquidación de compra de Bienes o Prestación de servicios", false) },
                { "04", ("N/C Compras", true) },
                { "05", ("N/D Compras", false) },
                { "08", ("Boletos o entradas a espectáculos públicos", false) },
                { "09", ("Tiquetes o vales emitidos por máquinas registradoras", false) },
                { "11", ("Pasajes expedidos por empresas de aviación", false) },
                { "12", ("Documentos emitidos por instituciones financieras", false) },
                { "13", ("Documentos emitidos por compañías de seguros", false) },
                { "14", ("Documentos emitidos por empresas de telecomunicaciones", false) },
                { "15", ("Comprobantes de venta emitidos en exterior", false) },
                { "19", ("Comprobantes de Pago de Cuotas o Aportes", false) },
                { "20", ("Documentos por Servicios Administrativos emitidos por Inst. del Estado", false) },
                { "21", ("Carta de Porte Aéreo", false) },
                { "41", ("Comprobante de venta emitido por reembolso", false) },
                { "47", ("N/C por Reembolso Emitida por Intermediario", true) },
                { "48", ("N/D por Reembolso Emitida por Intermediario", false) }
            };

            var ventasTipos = new Dictionary<string, (string Transaccion, bool Negativo)>
            {
                { "01", ("Factura", false) },
                { "04", ("N/C Ventas", true) },
                { "05", ("N/D Ventas", false) },
                { "18", ("Documentos autorizados utilizados en ventas excepto N/C N/D", false) },
                { "44", ("Comprobante de contribuciones y aportes", false) },
                { "49", ("Proveedor Directo de Exportador Bajo Régimen Especial", false) },
                { "50", ("A Inst. Estado y Empr. Públicas que percibe ingreso exento de Imp. Renta", false) },
                { "51", ("N/C A Inst. Estado y Empr. Públicas que percibe ingreso exento de Imp. Renta", true) },
                { "52", ("N/D A Inst. Estado y Empr. Públicas que percibe ingreso exento de Imp. Renta", false) }
            };

            foreach (var tipo in comprasTipos)
            {
                var data = comprasAgrupadas.GetValueOrDefault(tipo.Key);
                ComprasResumen.Add(new ResumenFila
                {
                    Codigo = tipo.Key,
                    Transaccion = tipo.Value.Transaccion,
                    NumRegistros = data?.Count ?? 0,
                    Base0 = data?.Base0 ?? 0,
                    BaseGrav = data?.BaseGrav ?? 0,
                    BaseNoGraIva = data?.BaseNoGraIva ?? 0,
                    MontoIva = data?.MontoIva ?? 0,
                    EsNegativo = tipo.Value.Negativo
                });
            }

            foreach (var tipo in ventasTipos)
            {
                var data = ventasAgrupadas.GetValueOrDefault(tipo.Key);
                VentasResumen.Add(new ResumenFila
                {
                    Codigo = tipo.Key,
                    Transaccion = tipo.Value.Transaccion,
                    NumRegistros = data?.Count ?? 0,
                    Base0 = data?.Base0 ?? 0,
                    BaseGrav = data?.BaseGrav ?? 0,
                    BaseNoGraIva = data?.BaseNoGraIva ?? 0,
                    MontoIva = data?.MontoIva ?? 0,
                    EsNegativo = tipo.Value.Negativo
                });
            }

            TotalComprasBase0 = ComprasResumen.Sum(x => x.Base0);
            TotalComprasBaseGrav = ComprasResumen.Sum(x => x.BaseGrav);
            TotalComprasBaseNoGraIva = ComprasResumen.Sum(x => x.BaseNoGraIva);
            TotalComprasIva = ComprasResumen.Sum(x => x.MontoIva);

            TotalVentasBase0 = VentasResumen.Sum(x => x.Base0);
            TotalVentasBaseGrav = VentasResumen.Sum(x => x.BaseGrav);
            TotalVentasBaseNoGraIva = VentasResumen.Sum(x => x.BaseNoGraIva);
            TotalVentasIva = VentasResumen.Sum(x => x.MontoIva);

            var retencionesQuery = _db.RetencionesClientes.Where(r => r.RucEmpresa == RucEmpresa);
            if (Anio > 0)
                retencionesQuery = retencionesQuery.Where(r => r.Anio == Anio);
            if (Mes > 0)
                retencionesQuery = retencionesQuery.Where(r => r.Mes == Mes);

            var retenciones = await retencionesQuery.ToListAsync();

            TotalRetIva10 = retenciones.Sum(r => r.ValRetBien10 ?? 0);
            TotalRetIva20 = retenciones.Sum(r => r.ValRetServ20 ?? 0);
            TotalRetIva30 = retenciones.Sum(r => r.ValorRetBienes ?? 0);
            TotalRetIva50 = retenciones.Sum(r => r.ValRetServ50 ?? 0);
            TotalRetIva70 = retenciones.Sum(r => r.ValorRetServicios ?? 0);
            TotalRetIva100 = retenciones.Sum(r => r.ValRetServ100 ?? 0);
            TotalRetIvaRecibida = retenciones.Sum(r => r.ValRetIva ?? 0);

            // Retenciones recibidas = ValRetRenta from RetencionesClientes (retenciones certificates received from suppliers)
            TotalRetRentaRecibida = retenciones.Sum(r => r.ValRetRenta ?? 0);
            
            // Retenciones emitidas = ValRetRenta from Ventas table (retenciones we made on our sales as withholding agent)
            var ventasConRetenciones = await _db.Ventas
                .Where(v => v.RucEmpresa == RucEmpresa)
                .Where(v => Anio == 0 || v.Anio == Anio)
                .Where(v => Mes == 0 || v.Mes == Mes)
                .ToListAsync();
            
            TotalRetRentaEmitida = ventasConRetenciones.Sum(v => v.valRetRenta ?? 0);
        }

        public async Task<IActionResult> OnPostExportarExcelAsync()
        {
            await CargarDatosAsync();

            using var workbook = new XLWorkbook();
            
            var wsCompras = workbook.Worksheets.Add("Compras");
            wsCompras.Cell(1, 1).Value = "Código";
            wsCompras.Cell(1, 2).Value = "Transacción";
            wsCompras.Cell(1, 3).Value = "N° Registros";
            wsCompras.Cell(1, 4).Value = "BI Tarifa 0%";
            wsCompras.Cell(1, 5).Value = "BI Tarifa Diferente 0%";
            wsCompras.Cell(1, 6).Value = "BI No Objeto IVA";
            wsCompras.Cell(1, 7).Value = "Valor IVA";
            for (int i = 1; i <= 7; i++)
            {
                wsCompras.Cell(1, i).Style.Font.Bold = true;
                wsCompras.Cell(1, i).Style.Fill.BackgroundColor = XLColor.LightGray;
            }

            int row = 2;
            foreach (var item in ComprasResumen)
            {
                wsCompras.Cell(row, 1).Value = item.Codigo;
                wsCompras.Cell(row, 2).Value = item.Transaccion;
                wsCompras.Cell(row, 3).Value = item.NumRegistros;
                wsCompras.Cell(row, 4).Value = item.Base0;
                wsCompras.Cell(row, 5).Value = item.BaseGrav;
                wsCompras.Cell(row, 6).Value = item.BaseNoGraIva;
                wsCompras.Cell(row, 7).Value = item.MontoIva;
                row++;
            }

            var wsVentas = workbook.Worksheets.Add("Ventas");
            wsVentas.Cell(1, 1).Value = "Código";
            wsVentas.Cell(1, 2).Value = "Transacción";
            wsVentas.Cell(1, 3).Value = "N° Registros";
            wsVentas.Cell(1, 4).Value = "BI Tarifa 0%";
            wsVentas.Cell(1, 5).Value = "BI Tarifa Diferente 0%";
            wsVentas.Cell(1, 6).Value = "BI No Objeto IVA";
            wsVentas.Cell(1, 7).Value = "Valor IVA";
            for (int i = 1; i <= 7; i++)
            {
                wsVentas.Cell(1, i).Style.Font.Bold = true;
                wsVentas.Cell(1, i).Style.Fill.BackgroundColor = XLColor.LightGray;
            }

            row = 2;
            foreach (var item in VentasResumen)
            {
                wsVentas.Cell(row, 1).Value = item.Codigo;
                wsVentas.Cell(row, 2).Value = item.Transaccion;
                wsVentas.Cell(row, 3).Value = item.NumRegistros;
                wsVentas.Cell(row, 4).Value = item.Base0;
                wsVentas.Cell(row, 5).Value = item.BaseGrav;
                wsVentas.Cell(row, 6).Value = item.BaseNoGraIva;
                wsVentas.Cell(row, 7).Value = item.MontoIva;
                row++;
            }

            wsCompras.Columns().AdjustToContents();
            wsVentas.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"TalonResumen_{RucEmpresa}_{Anio}_{Mes:00}.xlsx");
        }

        private async Task CargarDatosAsync()
        {
            var empresas = await _db.Empresas.OrderBy(e => e.RazonSocial).ToListAsync();
            Empresas = empresas;

            if (!string.IsNullOrWhiteSpace(RucEmpresa))
            {
                await CargarResumenAsync();
            }
        }

        public class ResumenFila
        {
            public string Codigo { get; set; } = "";
            public string Transaccion { get; set; } = "";
            public int NumRegistros { get; set; }
            public decimal Base0 { get; set; }
            public decimal BaseGrav { get; set; }
            public decimal BaseNoGraIva { get; set; }
            public decimal MontoIva { get; set; }
            public bool EsNegativo { get; set; }
        }
    }
}
