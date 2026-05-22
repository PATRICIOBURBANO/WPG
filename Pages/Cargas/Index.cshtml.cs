using AtsManager.Models;
using AtsManager.Pages.Empresas.Models;
using AtsManager.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AtsManager.Pages.Cargas
{
    // Clase auxiliar para facilitar la clonación de entidades
    public static class EntityExtensions
    {
        public static Venta CloneForUpdate(this Venta original)
        {
            return new Venta
            {
                Id = original.Id,
                Anio = original.Anio,
                Mes = original.Mes,
                RucEmpresa = original.RucEmpresa,
                TipoComprobante = original.TipoComprobante,
                NumComprobante = original.NumComprobante,
                FechaEmision = original.FechaEmision,
                IdCliente = original.IdCliente,
                RazonSocialCliente = original.RazonSocialCliente,
                MontoTotal = original.MontoTotal,
                BaseImponible = original.BaseImponible,
                MontoIva = original.MontoIva,
                FormaPago = original.FormaPago,
                valRetRenta = original.valRetRenta,
                valRetIVA = original.valRetIVA,
                NumRetencion = original.NumRetencion,
                FechaRetencion = original.FechaRetencion,
                AutorizacionRetencion = original.AutorizacionRetencion,
                CargaLoteId = original.CargaLoteId,
                UsuarioCreacion = original.UsuarioCreacion,
                FechaCreacion = original.FechaCreacion,
            };
        }
    }

    public class IndexModel : PageModel
    {
        private readonly AtsDbContext _db;
        private readonly CultureInfo _loadCulture = new CultureInfo("es-EC");
        private readonly ILogger<IndexModel> _logger;
        private readonly XmlBatchImporter _xmlBatchImporter;
        [BindProperty]
        public IFormFile? ArchivoCarga { get; set; }

        [BindProperty]
        public int Anio { get; set; } = DateTime.Now.Year;

        [BindProperty]
        public int Mes { get; set; } = DateTime.Now.Month;

        [BindProperty]
        [Required(ErrorMessage = "Seleccione la empresa.")]
        public int EmpresaId { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "Seleccione el tipo de anexo.")]
        public string TipoArchivo { get; set; } = string.Empty;

        [BindProperty]
        public bool ConfirmarReemplazo { get; set; }
        [TempData]
        public string MensajeCarga { get; set; } = string.Empty;
        public List<string> ErroresValidacion { get; set; } = new List<string>();
        [BindProperty]
        public List<IFormFile> XmlFiles { get; set; } = new();

        [BindProperty]
        public string ContextoCarga { get; set; } = "RECIBIDOS";
        public IList<CargaLote> CargasLotes { get; set; } = new List<CargaLote>();
        public List<Empresa> Empresas { get; set; } = new List<Empresa>();

        
        public async Task OnGetAsync()
        {
            Empresas = await _db.Empresas.Where(e => e.Activa).OrderBy(e => e.RazonSocial).ToListAsync();
            CargasLotes = await _db.CargasLotes
                .OrderByDescending(c => c.Anio)
                .ThenByDescending(c => c.Mes)
                .ThenByDescending(c => c.FechaCarga)
                .ToListAsync();
        }
        public IndexModel(
    AtsDbContext context,
    ILogger<IndexModel> logger,
    XmlBatchImporter xmlBatchImporter
)
        {
            _db = context;
            _logger = logger;
            _xmlBatchImporter = xmlBatchImporter;
        }
        public List<string> ResultadosImportacion { get; set; } = new();
        
        public async Task<IActionResult> OnPostAsync()
        {
            if (EmpresaId <= 0)
            {
                MensajeCarga = "Por favor seleccione una empresa.";
                await OnGetAsync();
                return Page();
            }

            var empresa = await _db.Empresas.FindAsync(EmpresaId);
            if (empresa == null)
            {
                MensajeCarga = "La empresa seleccionada no existe.";
                await OnGetAsync();
                return Page();
            }

            // Validación condicional: permitir TXT/CSV o carpeta de XML
            if (string.IsNullOrWhiteSpace(TipoArchivo))
            {
                MensajeCarga = "Por favor seleccione el tipo de anexo.";
                await OnGetAsync();
                return Page();
            }

            bool tieneTxt = ArchivoCarga != null && ArchivoCarga.Length > 0;
            bool tieneXml = XmlFiles != null && XmlFiles.Any();

            if (!tieneTxt && !tieneXml)
            {
                MensajeCarga = "Debe seleccionar un archivo TXT/CSV o una carpeta con XML.";
                await OnGetAsync();
                return Page();
            }

            // ------------------------------------------------------------
            // Confirmación de reemplazo (mismo período + mismo tipo)
            // ------------------------------------------------------------
            var loteExistente = await _db.CargasLotes
                .OrderByDescending(x => x.FechaCarga)
                .FirstOrDefaultAsync(x => x.Anio == Anio && x.Mes == Mes && x.TipoArchivo == TipoArchivo);

            if (loteExistente != null && !ConfirmarReemplazo)
            {
                ConfirmarReemplazo = true;
                MensajeCarga = $"⚠️ Ya existe una carga para {Anio}/{Mes:00} - {TipoArchivo}. ¿Desea reemplazarla?";
                await OnGetAsync();
                return Page();
            }

            // Si confirmó reemplazo, eliminamos el lote anterior (y sus registros) antes de cargar
            if (loteExistente != null && ConfirmarReemplazo)
            {
                await OnPostDeleteLoteAsync(loteExistente.Id);
                // OnPostDeleteLoteAsync hace Redirect; aquí solo limpiamos el estado para seguir
                _db.ChangeTracker.Clear();
            }

            // ------------------------------------------------------------
            // MODO 1: IMPORTAR DESDE XML (carpeta)
            // ------------------------------------------------------------
            if (tieneXml && !tieneTxt)
            {
                // 1️⃣ Crear carpeta temporal
                string tempPath = Path.Combine(
                    Path.GetTempPath(),
                    $"XML_IMPORT_{Anio}_{Mes}_{Guid.NewGuid()}"
                );

                Directory.CreateDirectory(tempPath);

                // 2️⃣ Guardar XML en carpeta temporal
                foreach (var file in XmlFiles)
                {
                    if (!file.FileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string filePath = Path.Combine(tempPath, Path.GetFileName(file.FileName));
                    using var stream = new FileStream(filePath, FileMode.Create);
                    await file.CopyToAsync(stream);
                }

                // 3️⃣ Ejecutar importador centralizado
                List<string> resultados;
                try
                {
                    resultados = _xmlBatchImporter.ImportarDesdeCarpeta(tempPath, ContextoCarga);
                }
                catch (Exception ex)
                {
                    MensajeCarga = $"❌ Error crítico durante la importación: {ex.Message}";
                    await OnGetAsync();
                    return Page();
                }

                // 4️⃣ Mostrar resultados
                ResultadosImportacion = resultados;

                MensajeCarga = resultados.Any(r => r.StartsWith("❌"))
                    ? "⚠️ La importación finalizó con errores."
                    : "✅ Importación XML completada correctamente.";

                // Refresh the lotes list
                await OnGetAsync();
                return Page();
            }

            // ------------------------------------------------------------
            // MODO 2: IMPORTAR DESDE TXT/CSV (SRI)
            // ------------------------------------------------------------
            try
            {
                // Crear lote
                var lote = new CargaLote
                {
                    Anio = Anio,
                    Mes = Mes,
                    TipoArchivo = TipoArchivo,
                    FechaCarga = DateTime.Now,
                    NombreArchivo = ArchivoCarga!.FileName,
                    TotalRegistros = 0,
                    TipoDocumento = "TXT/CSV"
                };

                _db.CargasLotes.Add(lote);
                await _db.SaveChangesAsync();

                // Leer archivo
                var (headers, rows) = await LeerTablaAsync(ArchivoCarga!);

                int total = 0;

                if (TipoArchivo.Equals("Compras", StringComparison.OrdinalIgnoreCase))
                {
                    var compras = rows
                        .Select(r => MapearCompraDesdeHeader(headers, r, Anio, Mes, lote.Id, empresa.Ruc))
                        .Where(x => x != null)
                        .Select(x => x!)
                        .ToList();

                    total = compras.Count;
                    _db.Compras.AddRange(compras);
                }
                else if (TipoArchivo.Equals("NCCompras", StringComparison.OrdinalIgnoreCase))
                {
                    var ncs = rows
                        .Select(r => MapearNCCompraDesdeHeader(headers, r, Anio, Mes, lote.Id, empresa.Ruc))
                        .Where(x => x != null)
                        .Select(x => x!)
                        .ToList();

                    total = ncs.Count;
                    _db.NCCompras.AddRange(ncs);
                }
                else if (TipoArchivo.Equals("Ventas", StringComparison.OrdinalIgnoreCase))
                {
                    var ventas = rows
                        .Select(r => MapearVentaDesdeHeader(headers, r, Anio, Mes, lote.Id, empresa.Ruc))
                        .Where(x => x != null)
                        .Select(x => x!)
                        .ToList();

                    total = ventas.Count;
                    _db.Ventas.AddRange(ventas);
                }
                else if (TipoArchivo.Equals("Retenciones", StringComparison.OrdinalIgnoreCase))
                {
                    int cargaLoteId = lote.Id;
                    
                    var rets = rows
                        .Select(r => MapearRetencionDesdeHeader(headers, r, Anio, Mes, cargaLoteId, empresa.Ruc))
                        .Where(x => x != null)
                        .Select(x => x!)
                        .ToList();

                    total = rets.Count;
                    
                    if (ContextoCarga == "RECIBIDOS")
                    {
                        var retsCompra = rets.Select(r => new RetencionCompra
                        {
                            CargaLoteId = cargaLoteId,
                            RucEmpresa = r.RucEmpresa,
                            RazonSocialProveedor = r.RazonSocialCliente,
                            DocAfectado = r.DocAfectado ?? "",
                            FechaDocAfectado = r.FechaDocAfectado,
                            NumRetencionCompleto = r.NumRetencionCompleto ?? "",
                            NumRetencion = r.NumRetencion ?? "",
                            Autorizacion = r.AutorizacionRetencion ?? "",
                            FechaRetencion = r.FechaRetencion,
                            BaseImpGrav = r.BaseImpGrav ?? 0,
                            MontoIva = r.MontoIva ?? 0,
                            BaseImpAir = r.BaseImpAir ?? 0,
                            ValRetBien10 = r.ValRetBien10 ?? 0,
                            ValRetServ20 = r.ValRetServ20 ?? 0,
                            ValorRetBienes = r.ValorRetBienes ?? 0,
                            ValRetServ50 = r.ValRetServ50 ?? 0,
                            ValorRetServicios = r.ValorRetServicios ?? 0,
                            ValRetServ100 = r.ValRetServ100 ?? 0,
                            ValRetIva = r.ValRetIva ?? 0,
                            CodRetAir = r.CodRetAir ?? "332",
                            PorcentajeAir = r.PorcentajeAir ?? 0,
                            ValRetRenta = r.ValRetRenta ?? 0,
                            TotalRetencion = (r.ValRetIva ?? 0) + (r.ValRetRenta ?? 0),
                            Anio = (short)Anio,
                            Mes = (short)Mes,
                            UsuarioCreacion = User?.Identity?.Name ?? "SYSTEM",
                            FechaCreacion = DateTime.Now
                        }).ToList();
                        _db.RetencionesCompras.AddRange(retsCompra);
                    }
                    else
                    {
                        _db.RetencionesClientes.AddRange(rets);
                    }
                }
                else
                {
                    MensajeCarga = $"❌ Tipo de anexo no soportado en TXT/CSV: {TipoArchivo}";
                    await OnGetAsync();
                    return Page();
                }

                // Guardar registros + actualizar lote
                await _db.SaveChangesAsync();
                lote.TotalRegistros = total;
                await _db.SaveChangesAsync();

                MensajeCarga = $"✅ Carga TXT/CSV completada. Registros importados: {total}.";
                return Page();
            }
            catch (Exception ex)
            {
                MensajeCarga = $"❌ Error al procesar TXT/CSV: {ex.Message}";
                await OnGetAsync();
                return Page();
            }
        }

        // --------------------------------------------------------------------
        // LECTURA TABULAR ROBUSTA (TAB / CSV / ;)
        // --------------------------------------------------------------------
        private async Task<(Dictionary<string, int> headers, List<string[]> rows)> LeerTablaAsync(IFormFile file)
        {
            using var stream = file.OpenReadStream();
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            string? firstLine = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(firstLine))
                throw new Exception("El archivo está vacío o no tiene cabecera.");

            char sep = firstLine.Contains('\t') ? '\t'
                    : firstLine.Contains(';') ? ';'
                    : ',';

            var headerParts = firstLine.Split(sep).Select(h => (h ?? "").Trim()).ToArray();
            var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headerParts.Length; i++)
            {
                var key = headerParts[i];
                if (!string.IsNullOrWhiteSpace(key) && !headers.ContainsKey(key))
                    headers[key] = i;
            }

            var rows = new List<string[]>();
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split(sep);
                rows.Add(parts);
            }

            return (headers, rows);
        }

        private static string Get(Dictionary<string, int> headers, string[] row, params string[] keys)
        {
            foreach (var k in keys)
            {
                if (headers.TryGetValue(k, out int idx))
                {
                    if (idx >= 0 && idx < row.Length) return (row[idx] ?? "").Trim();
                }
            }
            return string.Empty;
        }

        private static DateTime? ParseDate(string s, CultureInfo culture)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            // SRI suele venir: dd/MM/yyyy o dd/MM/yyyy HH:mm:ss
            if (DateTime.TryParse(s, culture, DateTimeStyles.None, out var dt)) return dt;
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)) return dt;
            return null;
        }

        private static decimal? ParseDecimal(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (decimal.TryParse(s.Trim().Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) return d;
            if (decimal.TryParse(s, NumberStyles.Any, new CultureInfo("es-EC"), out d)) return d;
            return null;
        }

        private static decimal? ParseDecimal(string s, CultureInfo culture)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var valorLimpio = s.Trim().Replace(',', '.');
            if (decimal.TryParse(valorLimpio, NumberStyles.Any, culture, out var d)) return d;
            if (decimal.TryParse(valorLimpio, NumberStyles.Any, CultureInfo.InvariantCulture, out d)) return d;
            if (decimal.TryParse(valorLimpio, NumberStyles.Any, new CultureInfo("es-EC"), out d)) return d;
            return null;
        }

        private static string CodigoTipoComprobanteDesdeTexto(string tipoTexto)
        {
            var t = (tipoTexto ?? "").Trim().ToUpperInvariant();

            if (t.Contains("NOTA DE CR") || t.Contains("NOTA DE CRÉ") || t.Contains("NOTA DE CRED")) return "04";
            if (t.Contains("NOTA DE D") || t.Contains("NOTA DE DÉ") || t.Contains("NOTA DE DEB")) return "05";
            if (t.Contains("LIQUIDACI")) return "03";
            if (t.Contains("NOTA DE VENTA")) return "02";
            if (t.Contains("RETEN")) return "07";
            return "01"; // Factura
        }

        // --------------------------------------------------------------------
        // MAPEOS POR HEADER (SRI)
        // --------------------------------------------------------------------
        private Compra? MapearCompraDesdeHeader(Dictionary<string, int> headers, string[] row, int anio, int mes, int cargaLoteId, string rucEmpresa)
        {
            // Formato típico SRI (emitidos/recibidos): RUC_EMISOR, RAZON_SOCIAL_EMISOR, TIPO_COMPROBANTE, SERIE_COMPROBANTE, CLAVE_ACCESO, FECHA_AUTORIZACION, FECHA_EMISION, IDENTIFICACION_RECEPTOR, VALOR_SIN_IMPUESTOS, IVA, IMPORTE_TOTAL
            var campos = new string[12];
            campos[0] = Get(headers, row, "RUC_EMISOR", "RUC_PROVEEDOR", "IDENTIFICACION_PROVEEDOR");
            campos[1] = Get(headers, row, "RAZON_SOCIAL_EMISOR", "RAZON_SOCIAL_PROVEEDOR", "NOMBRE_PROVEEDOR");
            campos[2] = Get(headers, row, "TIPO_COMPROBANTE", "TIPO_COMPROBANTE_SUSTENTO");
            campos[3] = Get(headers, row, "SERIE_COMPROBANTE", "NUMERO_COMPROBANTE", "NUM_COMPROBANTE");
            campos[4] = Get(headers, row, "CLAVE_ACCESO", "CLAVE_ACCESO_SUSTENTO");
            campos[5] = Get(headers, row, "FECHA_AUTORIZACION", "FECHA_AUTORIZA");
            campos[6] = Get(headers, row, "FECHA_EMISION", "FECHA");
            campos[7] = Get(headers, row, "IDENTIFICACION_RECEPTOR", "RUC_RECEPTOR");
            campos[8] = Get(headers, row, "VALOR_SIN_IMPUESTOS", "BASE_IMPONIBLE", "BASE");
            campos[9] = Get(headers, row, "IVA", "MONTO_IVA");
            campos[10] = Get(headers, row, "IMPORTE_TOTAL", "TOTAL");
            campos[11] = Get(headers, row, "NUMERO_DOCUMENTO_MODIFICADO", "DOC_MODIFICADO");

            var compra = MapearCompra(campos, anio, mes, rucEmpresa);
            if (compra == null) return null;
            compra.CargaLoteId = cargaLoteId;
            compra.FechaCreacion = DateTime.Now;
            compra.UsuarioCreacion = User?.Identity?.Name ?? "SYSTEM";
            compra.Anio = (short)anio;
            compra.Mes = (short)mes;
            compra.RucEmpresa = rucEmpresa;

            // Clave de acceso / autorización
            compra.Autorizacion = campos[4];
            compra.FechaRegistro = ParseDate(campos[5], _loadCulture);

            // Desglose establecimiento / punto / secuencial si viene serie "001-003-000000075"
            var serie = campos[3];
            if (!string.IsNullOrWhiteSpace(serie) && serie.Contains("-"))
            {
                var parts = serie.Split('-', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    compra.Estab = parts[0];
                    compra.PtoEmi = parts[1];
                    compra.Secuencial = parts[2];
                }
            }

            return compra;
        }

        private NCCompra? MapearNCCompraDesdeHeader(Dictionary<string, int> headers, string[] row, int anio, int mes, int cargaLoteId, string rucEmpresa)
        {
            var clave = Get(headers, row, "CLAVE_ACCESO");
            if (string.IsNullOrWhiteSpace(clave)) return null;

            var fechaAut = ParseDate(Get(headers, row, "FECHA_AUTORIZACION"), _loadCulture) ?? DateTime.Now;
            var fechaEmi = ParseDate(Get(headers, row, "FECHA_EMISION"), _loadCulture) ?? DateTime.Now;

            var serie = Get(headers, row, "SERIE_COMPROBANTE", "NUMERO_COMPROBANTE");
            var numDocMod = Get(headers, row, "NUMERO_DOCUMENTO_MODIFICADO", "NUMERO_DOCUMENTO_MODIFICAD", "DOC_MODIFICADO");

            var valorSin = ParseDecimal(Get(headers, row, "VALOR_SIN_IMPUESTOS")) ?? 0m;
            var iva = ParseDecimal(Get(headers, row, "IVA")) ?? 0m;
            var total = ParseDecimal(Get(headers, row, "IMPORTE_TOTAL", "TOTAL")) ?? (valorSin + iva);

            return new NCCompra
            {
                ClaveAcceso = clave,
                FechaAutorizacion = fechaAut,
                FechaEmision = fechaEmi,
                SerieComprobante = serie,
                NumeroDocumentoModificado = numDocMod,
                RucEmisor = rucEmpresa,
                RazonSocialEmisor = Get(headers, row, "RAZON_SOCIAL_EMISOR"),
                ValorSinImpuestos = valorSin,
                IVA = iva,
                Total = total,
                Anio = anio,
                Mes = mes,
                FechaRegistro = DateTime.Now,
                UsuarioCreacion = User?.Identity?.Name ?? "SYSTEM",
                FechaCreacion = DateTime.Now
            };
        }

        private Venta? MapearVentaDesdeHeader(Dictionary<string, int> headers, string[] row, int anio, int mes, int cargaLoteId, string rucEmpresa)
        {
            var tipoTexto = Get(headers, row, "TIPO_COMPROBANTE");
            var codigoTipo = CodigoTipoComprobanteDesdeTexto(tipoTexto);

            var serie = Get(headers, row, "SERIE_COMPROBANTE", "NUMERO_COMPROBANTE", "NUM_COMPROBANTE");
            var fechaEmi = ParseDate(Get(headers, row, "FECHA_EMISION"), _loadCulture);

            var idCliente = Get(headers, row, "IDENTIFICACION_RECEPTOR", "IDENTIFICACION_COMPRADOR", "RUC_RECEPTOR");
            var razon = Get(headers, row, "RAZON_SOCIAL_RECEPTOR", "RAZON_SOCIAL_COMPRADOR", "NOMBRE_RECEPTOR");

            var baseSin = ParseDecimal(Get(headers, row, "VALOR_SIN_IMPUESTOS", "BASE_IMPONIBLE")) ?? 0m;
            var iva = ParseDecimal(Get(headers, row, "IVA")) ?? 0m;
            var total = ParseDecimal(Get(headers, row, "IMPORTE_TOTAL", "TOTAL")) ?? (baseSin + iva);

            var v = new Venta
            {
                Anio = (short)anio,
                Mes = (short)mes,
                FechaCreacion = DateTime.Now,
                UsuarioCreacion = User?.Identity?.Name ?? "SYSTEM",
                CargaLoteId = cargaLoteId,
                ClaveAcceso = Get(headers, row, "CLAVE_ACCESO"),
                TipoComprobante = codigoTipo,
                FechaEmision = fechaEmi,
                NumComprobante = (serie ?? "").Replace("-", ""),
                IdCliente = idCliente,
                RazonSocialCliente = razon,
                BaseImponible = baseSin,
                MontoIva = iva,
                MontoTotal = total,
                FormaPago = "20",
                RucEmpresa = rucEmpresa
            };

            // tipo id cliente
            v.TipoIdCliente = idCliente.Length == 13 ? "04" : idCliente.Length == 10 ? "05" : "06";

            if (!string.IsNullOrWhiteSpace(serie) && serie.Contains("-"))
            {
                var parts = serie.Split('-', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    v.Estab = parts[0];
                    v.PtoEmi = parts[1];
                    v.Secuencial = parts[2];
                }
            }

            return v;
        }

        private RetencionCliente? MapearRetencionDesdeHeader(Dictionary<string, int> headers, string[] row, int anio, int mes, int cargaLoteId, string rucEmpresa)
        {
            var numRet = Get(headers, row, "NUMERO_COMPROBANTE", "SERIE_COMPROBANTE", "NUMERO_RETENCION", "NUM_RETENCION");
            var clave = Get(headers, row, "CLAVE_ACCESO", "CLAVE_ACCESO_RETENCION");
            if (string.IsNullOrWhiteSpace(numRet) && string.IsNullOrWhiteSpace(clave)) return null;

            var fechaAut = ParseDate(Get(headers, row, "FECHA_AUTORIZACION"), _loadCulture);
            var fechaEmi = ParseDate(Get(headers, row, "FECHA_EMISION", "FECHA_RETENCION"), _loadCulture) ?? DateTime.Now;

            var razon = Get(headers, row, "RAZON_SOCIAL_EMISOR", "NOMBRE_EMISOR");
            var idCliente = Get(headers, row, "RUC_EMISOR", "IDENTIFICACION_EMISOR", "ID_CLIENTE");
            var docAfectado = Get(headers, row, "DOCUMENTO_AFECTADO", "FACTURA_AFECTADA", "CLAVE_ACCESO_SUSTENTO", "CLAVE_SUSTENTO");
            var fechaDocAfectado = ParseDate(Get(headers, row, "FECHA_DOCUMENTO_AFECTADO", "FECHA_FACTURA"), _loadCulture);

            var baseImpGrav = ParseDecimal(Get(headers, row, "BASE_IMPONIBLE_GRAVADA", "BASE_GRAVADA", "BASE_IVA", "MONTO_BASE_IVA"), _loadCulture);
            var montoIva = ParseDecimal(Get(headers, row, "MONTO_IVA", "IVA", "IMPUESTO_IVA"), _loadCulture);
            var baseImpAir = ParseDecimal(Get(headers, row, "BASE_IMPONIBLE_RENTA", "BASE_RENTA", "BASE_AIR", "MONTO_BASE_AIR"), _loadCulture);
            var valRetIva = ParseDecimal(Get(headers, row, "VALOR_RETENCION_IVA", "RETENCION_IVA", "VALOR_IVA"), _loadCulture);
            var valRetRenta = ParseDecimal(Get(headers, row, "VALOR_RETENCION_RENTA", "RETENCION_RENTA", "VALOR_RENTA", "RETENCION"), _loadCulture);
            var porcentajeAir = ParseDecimal(Get(headers, row, "PORCENTAJE_AIR", "Pct_AIR", "PORCENTAJE"), _loadCulture);
            var codRetAir = Get(headers, row, "CODIGO_RETENCION", "COD_RETENCION", "COD_AIR", "COD_RET_AIR") ?? "332";

            var ret = new RetencionCliente
            {
                CargaLoteId = cargaLoteId,
                RucEmpresa = rucEmpresa,
                IdCliente = idCliente ?? "",
                RazonSocialCliente = razon ?? "",
                DocAfectado = docAfectado ?? "",
                FechaDocAfectado = fechaDocAfectado,
                NumRetencionCompleto = numRet ?? "",
                NumRetencion = (numRet ?? "").Replace("-", ""),
                AutorizacionRetencion = clave ?? "",
                FechaRetencion = fechaEmi,
                BaseImpGrav = baseImpGrav,
                MontoIva = montoIva,
                BaseImpAir = baseImpAir,
                ValRetIva = valRetIva,
                ValRetRenta = valRetRenta,
                PorcentajeAir = porcentajeAir,
                CodRetAir = codRetAir,
                TotalRetencion = (valRetIva ?? 0) + (valRetRenta ?? 0),
                Anio = (short)anio,
                Mes = (short)mes,
                UsuarioCreacion = User?.Identity?.Name ?? "SYSTEM",
                FechaCreacion = DateTime.Now
            };

            return ret;
        }



        // --------------------------------------------------------------------
        // MÉTODO DE MAPEO CORREGIDO PARA NOTAS DE CRÉDITO (TXT)
        // --------------------------------------------------------------------
        private Compra MapearCompra(string[] campos, int anio, int mes, string rucEmpresa)
        {
            // Índices (0-based):
            // 0:RUC, 1:RAZON, 2:TIPO, 3:SERIE, 4:CLAVE, 6:FECHA, 8:BASE, 9:IVA, 10:TOTAL

            try
            {
                // 1. DETECCIÓN DE TIPO (Maneja tildes y mayúsculas/minúsculas)
                string tipoTexto = campos[2].Trim().ToUpper();
                string codigoTipo = "01"; // Default Factura

                if (tipoTexto.Contains("NOTA DE CR") || tipoTexto.Contains("NOTA DE CRÉDITO")) codigoTipo = "04";
                else if (tipoTexto.Contains("NOTA DE D") || tipoTexto.Contains("NOTA DE DÉBITO") || tipoTexto.Contains("NOTA DE DEBITO")) codigoTipo = "05";
                else if (tipoTexto.Contains("LIQUIDACI")) codigoTipo = "03";
                else if (tipoTexto.Contains("NOTA DE VENTA")) codigoTipo = "02";

                // 2. NUMERO LIMPIO
                string numComprobanteLimpio = campos[3].Trim().Replace("-", "");

                // 3. PARSEO SEGURO DE MONTOS
                // Usamos InvariantCulture (punto para decimales) pero reemplazamos coma por punto por si acaso.
                decimal valorSinImpuestos = 0m;
                decimal montoIva = 0m;
                decimal montoTotal = 0m;

                if (!string.IsNullOrWhiteSpace(campos[8]))
                    decimal.TryParse(campos[8].Trim().Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out valorSinImpuestos);

                if (!string.IsNullOrWhiteSpace(campos[9]))
                    decimal.TryParse(campos[9].Trim().Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out montoIva);

                // Si el total (col 10) viene vacío, lo calculamos. Si viene lleno, lo usamos.
                if (string.IsNullOrWhiteSpace(campos[10]))
                {
                    montoTotal = valorSinImpuestos + montoIva;
                }
                else
                {
                    decimal.TryParse(campos[10].Trim().Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out montoTotal);
                }

                // 4. BASES
                decimal base12 = 0.00M;
                decimal base0 = 0.00M;
                string codSustento = "02"; // Default sin IVA

                if (montoIva > 0)
                {
                    const decimal IVA_RATE = 0.15M;

                    // Base gravada real según IVA
                    decimal baseGravadaCalculada = Math.Round(montoIva / IVA_RATE, 2);

                    // IVA esperado si TODA la base fuera gravada
                    decimal ivaEsperado = Math.Round(valorSinImpuestos * IVA_RATE, 2);

                    if (ivaEsperado == montoIva)
                    {
                        // Caso normal: todo gravado
                        base12 = valorSinImpuestos;
                        base0 = 0.00M;
                    }
                    else
                    {
                        // Caso MIXTO: base gravada + base exenta
                        base12 = baseGravadaCalculada;
                        base0 = Math.Round(valorSinImpuestos - base12, 2);

                        if (base0 < 0) base0 = 0.00M; // seguridad
                    }

                    codSustento = "01"; // con IVA
                }
                else
                {
                    // Sin IVA
                    base12 = 0.00M;
                    base0 = valorSinImpuestos;
                }


                // 5. FECHA (Manejo de error específico)
                string fechaStr = campos[6].Trim();
                // Intentamos limpiar la fecha si trae hora (ej: 31/10/2025 00:00:00)
                if (fechaStr.Contains(" ")) fechaStr = fechaStr.Split(' ')[0];

                if (!DateTime.TryParseExact(fechaStr, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime fechaEmision))
                {
                    // Intento secundario con formato local si falla el invariante
                    if (!DateTime.TryParse(fechaStr, out fechaEmision))
                    {
                        throw new Exception($"Fecha inválida: {fechaStr}");
                    }
                }

                // 6. RUC Y TIPO ID
                string ruc = campos[0].Trim();
                string tipoIdProv = "03";
                if (ruc.Length == 13) tipoIdProv = "01";
                else if (ruc.Length == 10) tipoIdProv = "02";

                return new Compra
                {
                    CodigoCompra = $"C{DateTime.Now.Ticks % 100000}",
                    Anio = (short)fechaEmision.Year,
                    Mes = (short)fechaEmision.Month,
                    RucEmpresa = rucEmpresa,

                    CodSustento = codSustento,
                    TipoIdProveedor = tipoIdProv,
                    IdProveedor = ruc,
                    RazonSocialProveedor = campos[1].Trim(),

                    TipoComprobante = codigoTipo,
                    NumComprobante = numComprobanteLimpio,
                    Autorizacion = campos[4].Trim(),

                    FechaRegistro = fechaEmision,
                    FechaEmision = fechaEmision,

                    BaseImponible = base0,
                    BaseImpGrav = base12,
                    BaseNoGraIva = 0.00M,
                    BaseImpExe = 0.00M,
                    MontoIva = montoIva,
                    MontoIce = 0.00M,
                    MontoTotal = montoTotal,

                    // Valores por defecto obligatorios
                    ValRetBien10 = 0.00M,
                    ValRetServ20 = 0.00M,
                    ValorRetBienes = 0.00M,
                    ValRetServ50 = 0.00M,
                    ValorRetServicios = 0.00M,
                    ValRetServ100 = 0.00M,
                    ValorRetencionNc = 0.00M,
                    BaseImpAir = 0.00M,
                    CodRetAir = "332",
                    PorcentajeAir = 0.00M,
                    ValRetAir = 0.00M,
                    PagoLocExt = "01",
                    FormaPago = "20",

                    UsuarioCreacion = "CargaMasivaTXT",
                    FechaCreacion = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                // Esto agregará detalles al mensaje de error en pantalla
                throw new Exception($"Error mapeando línea (RUC: {campos[0]}): {ex.Message}");
            }
        }

        private decimal ParseDecimalSafe(string val)
        {
            if (string.IsNullOrWhiteSpace(val)) return 0.00M;
            return decimal.TryParse(val.Trim().Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal res) ? res : 0.00M;
        }

        private List<NCCompra> ProcesarNotasCredito(string[] lineas, int anio, int mes)
        {
            var lista = new List<NCCompra>();

            if (lineas.Length <= 1)
                return lista;

            // leer encabezados
            var headers = lineas[0].Split('\t');

            int idxRuc = Array.IndexOf(headers, "RUC_EMISOR");
            int idxRazon = Array.IndexOf(headers, "RAZON_SOCIAL_EMISOR");
            int idxTipo = Array.IndexOf(headers, "TIPO_COMPROBANTE");
            int idxSerie = Array.IndexOf(headers, "SERIE_COMPROBANTE");
            int idxClave = Array.IndexOf(headers, "CLAVE_ACCESO");
            int idxFecAut = Array.IndexOf(headers, "FECHA_AUTORIZACION");
            int idxFecEmi = Array.IndexOf(headers, "FECHA_EMISION");
            int idxValor = Array.IndexOf(headers, "VALOR_SIN_IMPUESTOS");
            int idxIva = Array.IndexOf(headers, "IVA");
            int idxTotal = Array.IndexOf(headers, "IMPORTE_TOTAL");
            int idxDocMod = Array.IndexOf(headers, "NUMERO_DOCUMENTO_MODIFICADO");

            for (int i = 1; i < lineas.Length; i++)
            {
                var c = lineas[i].Split('\t');
                if (c.Length < headers.Length) continue;

                if (!c[idxTipo].Equals("Nota de Crédito", StringComparison.OrdinalIgnoreCase))
                    continue; // solo NC

                var nc = new NCCompra
                {
                    RucEmisor = c[idxRuc],
                    RazonSocialEmisor = c[idxRazon],
                    SerieComprobante = c[idxSerie],
                    ClaveAcceso = c[idxClave],
                    NumeroDocumentoModificado = c[idxDocMod],

                    FechaAutorizacion = DateTime.Parse(c[idxFecAut]),
                    FechaEmision = DateTime.Parse(c[idxFecEmi]),

                    ValorSinImpuestos = decimal.TryParse(c[idxValor], out var v1) ? v1 : 0,
                    IVA = decimal.TryParse(c[idxIva], out var v2) ? v2 : 0,
                    Total = decimal.TryParse(c[idxTotal], out var v3) ? v3 : (v1 + v2),

                    Anio = anio,
                    Mes = mes,
                    FechaRegistro = DateTime.Now,
                    FechaCreacion = DateTime.Now,
                    UsuarioCreacion = User?.Identity?.Name ?? "CargaMasiva"
                };

                lista.Add(nc);
            }

            return lista;
        }
        // --- LÓGICA DE RETENCIONES (Ventas) ---
        private async Task<(Venta? venta, string mensaje)> ProcesarRetencionRegistroAsync(string[] campos, int anio, int mes, int cargaLoteId)
        {
            string numComprobanteVenta = campos[11].Trim().Replace("-", "");
            string rucCliente = campos[0].Trim();

            var ventaExistente = await _db.Ventas.AsNoTracking()
                .FirstOrDefaultAsync(v => v.NumComprobante == numComprobanteVenta);

            DateTime fechaRetencion = DateTime.ParseExact(campos[6].Trim(), "dd/MM/yyyy", _loadCulture);

            if (ventaExistente != null)
            {
                var ventaUpdate = ventaExistente.CloneForUpdate();
                ventaUpdate.CargaLoteId = cargaLoteId;
                ventaUpdate.NumRetencion = campos[3].Trim().Replace("-", "");
                ventaUpdate.AutorizacionRetencion = campos[4].Trim();
                ventaUpdate.FechaRetencion = fechaRetencion;
                return (ventaUpdate, "ACTUALIZADA");
            }
            else
            {
                var ventaCero = new Venta
                {
                    Anio = (short)anio,
                    Mes = (short)mes,
                    CargaLoteId = cargaLoteId,
                    TipoComprobante = "01",
                    NumComprobante = numComprobanteVenta,
                    IdCliente = rucCliente,
                    RazonSocialCliente = campos[1].Trim(),
                    FechaEmision = fechaRetencion,
                    MontoTotal = 0.00M,
                    BaseImponible = 0.00M,
                    MontoIva = 0.00M,
                    FormaPago = "20",
                    valRetRenta = 0.00M,
                    valRetIVA = 0.00M, // Asignar valores si vienen en el TXT
                    NumRetencion = campos[3].Trim().Replace("-", ""),
                    AutorizacionRetencion = campos[4].Trim(),
                    FechaRetencion = fechaRetencion,
                    UsuarioCreacion = "CargaRetencionTXT",
                    FechaCreacion = DateTime.Now
                };
                return (ventaCero, "CREADA");
            }
        }
        private async Task<bool> DeleteLoteInternalAsync(int id)
        {
            var lote = await _db.CargasLotes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (lote == null)
            {
                MensajeCarga = "⚠️ El lote ya no existe (probablemente ya fue eliminado).";
                return false;
            }

            var strategy = _db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _db.Database.BeginTransactionAsync();
                try
                {
                    if (lote.TipoArchivo == "Compras")
                    {
                        await _db.Compras.Where(c => c.CargaLoteId == id).ExecuteDeleteAsync();
                    }
                    else if (lote.TipoArchivo == "Retenciones")
                    {
                        await _db.Ventas.Where(v => v.CargaLoteId == id && v.MontoTotal == 0).ExecuteDeleteAsync();

                        await _db.Database.ExecuteSqlRawAsync(@"
                    UPDATE Ventas
                    SET CargaLoteId = NULL,
                        NumRetencion = NULL,
                        valRetRenta = 0
                    WHERE CargaLoteId = {0}", id);
                    }
                    // Si tienes Ventas/otros tipos, agrega aquí sus deletes.

                    // Borra lote (sin tracking)
                    await _db.CargasLotes.Where(x => x.Id == id).ExecuteDeleteAsync();

                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });

            // Importante para evitar estado viejo en el mismo request
            _db.ChangeTracker.Clear();

            MensajeCarga = "✅ Lote eliminado correctamente.";
            return true;
        }

        public async Task<IActionResult> OnPostDeleteLoteAsync(int id)
        {
            var lote = await _db.CargasLotes.FindAsync(id);
            if (lote == null) return Page();

            var strategy = _db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _db.Database.BeginTransactionAsync();
                try
                {
                    if (lote.TipoArchivo == "Compras")
                    {
                        await _db.Compras.Where(c => c.CargaLoteId == id).ExecuteDeleteAsync();
                    }
                    else if (lote.TipoArchivo == "Retenciones")
                    {
                        await _db.Ventas.Where(v => v.CargaLoteId == id && v.MontoTotal == 0).ExecuteDeleteAsync();
                        // Resetear FK de ventas actualizadas
                        await _db.Database.ExecuteSqlRawAsync("UPDATE Ventas SET CargaLoteId = NULL, NumRetencion = NULL, valRetRenta = 0 WHERE CargaLoteId = {0}", id);
                    }
                    if (lote.TipoArchivo == "XML" || lote.TipoArchivo == "Ventas")
                    {
                        // 1️⃣ Eliminar ventas "cero" creadas por retenciones
                        await _db.Database.ExecuteSqlRawAsync(@"
        DELETE FROM Ventas
        WHERE CargaLoteId = {0}
          AND MontoTotal = 0
          AND BaseImponible = 0
    ", lote.Id);

                        // 2️⃣ Desacoplar ventas reales
                        await _db.Database.ExecuteSqlRawAsync(@"
        UPDATE Ventas
        SET CargaLoteId = NULL
        WHERE CargaLoteId = {0}
    ", lote.Id);
                    }

                    // 3️⃣ Ahora sí, borrar el lote
                    _db.CargasLotes.Remove(lote);
                    await _db.SaveChangesAsync();

                    await transaction.CommitAsync();
                    MensajeCarga = "✅ Lote eliminado correctamente.";
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    MensajeCarga = $"❌ Error al eliminar: {ex.Message}";
                }
            });

            return RedirectToPage();
        }
    }
}