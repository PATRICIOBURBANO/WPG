using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using AtsManager.Models;
using System.Linq; // Necesario para Linq
using AtsManager.Services;
using System.Threading.Tasks; // Necesario para Task
using System.Collections.Generic; // Necesario para List
using System.IO; // Necesario para StreamReader
using System; // Necesario para DateTime
// -----------------------------------------------------
// Se añaden los 'usings' faltantes de las correcciones anteriores
// -----------------------------------------------------
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using System.Data;
using Microsoft.EntityFrameworkCore.Storage;
using System.Text; // 🎯 CORRECCIÓN 1: Añadido el using para 'Encoding'
// -----------------------------------------------------

namespace AtsManager.Pages.Cargas
{
    public class IndexModel : PageModel
    {
        private readonly AtsDbContext _db;
        // -----------------------------------------------------
        // Se añaden ILogger y IHttpContextAccessor
        // -----------------------------------------------------
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<IndexModel> _logger;
        // -----------------------------------------------------
        private readonly CultureInfo _loadCulture = new CultureInfo("es-EC");

        // Propiedades para el Formulario de Carga
        [BindProperty]
        [Required(ErrorMessage = "Debe seleccionar un archivo para cargar.")]
        public IFormFile ArchivoCarga { get; set; } = default!;

        [BindProperty]
        public int Anio { get; set; } // CORREGIDO: Sin inicialización

        [BindProperty]
        public int Mes { get; set; } // CORREGIDO: Sin inicialización

        [BindProperty]
        [Required(ErrorMessage = "Debe seleccionar un tipo de anexo.")]
        public string TipoArchivo { get; set; } = default!;

        [BindProperty]
        public bool ConfirmarReemplazo { get; set; } // Añadido para el flujo de confirmación

        public string MensajeCarga { get; set; } = string.Empty;
        public List<string> ErroresValidacion { get; set; } = new List<string>();

        public IList<CargaLote> CargasLotes { get; set; } = new List<CargaLote>();

        // Constructor actualizado con Inyección de Dependencias
        public IndexModel(AtsDbContext context, IHttpContextAccessor httpContextAccessor, ILogger<IndexModel> logger)
        {
            _db = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        // OnGet actualizado con lógica de Sesión
        public async Task OnGetAsync()
        {
            int currentYear = DateTime.Now.Year;
            int currentMonth = DateTime.Now.Month;

            if (Anio == 0) Anio = currentYear;
            if (Mes == 0) Mes = currentMonth;

            var session = _httpContextAccessor.HttpContext?.Session;
            if (session != null)
            {
                if (Anio == currentYear && Mes == currentMonth)
                {
                    Anio = session.GetInt32("LastAnio") ?? currentYear;
                    Mes = session.GetInt32("LastMes") ?? currentMonth;
                }
                TipoArchivo = session.GetString("LastTipoArchivo") ?? TipoArchivo;
            }

            try
            {
                CargasLotes = await _db.CargasLotes
                    .OrderByDescending(c => c.Anio)
                    .ThenByDescending(c => c.Mes)
                    .ThenByDescending(c => c.FechaCarga)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener CargasLotes en OnGetAsync.");
                CargasLotes = new List<CargaLote>();
            }
        }

        // --------------------------------------------------------------------
        // ON POST (Handler Principal de Carga)
        // --------------------------------------------------------------------
        public async Task<IActionResult> OnPostAsync()
        {
            await OnGetAsync(); // Recarga la lista de lotes

            // Validamos que los valores bindeados (Anio y Mes) sean lógicos
            if (Anio <= 2000 || Mes <= 0 || Mes > 12)
            {
                MensajeCarga = "Error: El Año o Mes seleccionado no es válido.";
                return Page();
            }

            if (ArchivoCarga == null || ArchivoCarga.Length == 0)
            {
                MensajeCarga = "Debe seleccionar un archivo para cargar.";
                return Page();
            }
            if (string.IsNullOrEmpty(TipoArchivo))
            {
                MensajeCarga = "Debe seleccionar el tipo de anexo a cargar.";
                return Page();
            }

            // Guardar el filtro en sesión ANTES de procesar el archivo.
            var session = _httpContextAccessor.HttpContext?.Session;
            if (session != null)
            {
                session.SetInt32("LastAnio", Anio);
                session.SetInt32("LastMes", Mes);
                session.SetString("LastTipoArchivo", TipoArchivo);
            }

            int archivoMes = 0;
            int archivoAnio = 0;

            // ====================================================================
            // PASO 1. PRE-VALIDACIÓN DE CONSISTENCIA DE PERÍODO
            // ====================================================================
            ArchivoCarga.OpenReadStream().Seek(0, SeekOrigin.Begin);
            using (var reader = new StreamReader(ArchivoCarga.OpenReadStream(), Encoding.UTF8))
            {
                await reader.ReadLineAsync(); // 1. Saltar encabezado (Línea 1)
                string line2 = await reader.ReadLineAsync();

                if (line2 == null)
                {
                    MensajeCarga = $"❌ **ERROR CRÍTICO:** Carga Cancelada. El archivo está vacío.";
                    return Page();
                }

                try
                {
                    var campos = line2.Split('\t');
                    DateTime fechaValidacion;
                    int colIndexFecha;
                    int minCampos;

                    // 🎯 CORRECCIÓN: Lógica flexible de validación de campos
                    if (TipoArchivo.Equals("Ventas", StringComparison.OrdinalIgnoreCase))
                    {
                        minCampos = 8;
                        colIndexFecha = 4; // Columna FECHA_EMISION en Ventas
                    }
                    else // Compras o Retenciones
                    {
                        minCampos = 11;
                        colIndexFecha = 6; // Columna FECHA_EMISION en Compras/Retenciones
                    }

                    if (campos.Length < minCampos)
                    {
                        MensajeCarga = $"❌ **ERROR CRÍTICO ({TipoArchivo}):** La línea 2 no tiene el número esperado de campos (se esperaban {minCampos}, se encontraron {campos.Length}).";
                        return Page();
                    }

                    if (DateTime.TryParseExact(campos[colIndexFecha].Split(' ')[0], "dd/MM/yyyy", _loadCulture, DateTimeStyles.None, out fechaValidacion))
                    {
                        if (fechaValidacion.Year != Anio || fechaValidacion.Month != Mes)
                        {
                            MensajeCarga = $"❌ **ERROR CRÍTICO:** Carga Cancelada. La línea 2 tiene fecha **{fechaValidacion.ToString("MM/yyyy")}**, que no coincide con el período de carga seleccionado **{Mes}/{Anio}**.";
                            return Page();
                        }
                        archivoMes = fechaValidacion.Month;
                        archivoAnio = fechaValidacion.Year;
                    }
                    else
                    {
                        MensajeCarga = $"❌ **ERROR CRÍTICO:** Carga Cancelada. Formato de fecha inválido en la línea 2 (Valor: {campos[colIndexFecha]}).";
                        return Page();
                    }
                }
                catch (Exception ex)
                {
                    MensajeCarga = $"❌ **ERROR CRÍTICO:** Carga Cancelada. Error inesperado en la línea 2: {ex.Message}";
                    return Page();
                }
            } // Fin using reader (Validación)

            if (archivoMes == 0 || archivoAnio == 0)
            {
                MensajeCarga = $"❌ **ERROR CRÍTICO:** No se pudo determinar el período de carga.";
                return Page();
            }

            // ====================================================================
            // PASO 2. COMPROBACIÓN DE LOTE EXISTENTE
            // ====================================================================
            CargaLote loteEliminado = null;
            var loteExistente = await _db.CargasLotes.FirstOrDefaultAsync(c =>
                 c.Anio == archivoAnio && c.Mes == archivoMes && c.TipoArchivo == TipoArchivo);

            if (loteExistente != null && !ConfirmarReemplazo)
            {
                TempData["Anio"] = Anio;
                TempData["Mes"] = Mes;
                TempData["TipoArchivo"] = TipoArchivo;
                TempData["ArchivoCargaNombre"] = ArchivoCarga.FileName;
                TempData["MensajeConfirmacion"] = $"⚠️ ¡ATENCIÓN! Ya existe una carga de **{TipoArchivo}** para el período **{archivoMes}/{archivoAnio}** (Lote ID: {loteExistente.Id}). ¿Desea **eliminar el lote existente** y reemplazarlo?";
                return Page();
            }

            if (loteExistente != null && ConfirmarReemplazo)
            {
                // Usamos el handler de eliminación para limpiar el lote anterior
                await OnPostDeleteLoteAsync(loteExistente.Id);
                MensajeCarga = $"⚠️ Lote existente ({loteExistente.Id}) ELIMINADO. Procesando nueva carga...";
            }

            // ====================================================================
            // PASO 3. PROCESAMIENTO, MAPEO Y GUARDADO FINAL
            // ====================================================================
            var nuevoLote = new CargaLote
            {
                Anio = archivoAnio,
                Mes = archivoMes,
                TipoArchivo = TipoArchivo.ToUpperInvariant(),
                NombreArchivo = ArchivoCarga.FileName,
                FechaCarga = DateTime.Now,
                TotalRegistros = 0,
                TipoDocumento = TipoArchivo.ToUpperInvariant()
            };

            int registrosTotalesEnArchivo = 0;
            int registrosValidosParaInsercion = 0;
            int retencionesActualizadas = 0;
            int ventasCeroCreadas = 0;

            List<string> erroresLote = new List<string>();
            List<Compra> nuevasCompras = new List<Compra>();
            List<Venta> ventasCargadas = new List<Venta>(); // Usado para Retenciones Y Ventas

            ArchivoCarga.OpenReadStream().Seek(0, SeekOrigin.Begin);
            HashSet<(string NumComprobante, string IdProveedor)> comprobantesEnLote = new HashSet<(string, string)>();

            var strategy = _db.Database.CreateExecutionStrategy();
            await strategy.Execute(async () =>
            {
                using (IDbContextTransaction transaction = await _db.Database.BeginTransactionAsync())
                {
                    try
                    {
                        // 1. Añadir el lote principal (Padre) al contexto.
                        _db.CargasLotes.Add(nuevoLote);
                        await _db.SaveChangesAsync(); // Guardamos para obtener el ID real

                        int loteIdReal = nuevoLote.Id;

                        // 2. Procesamiento del archivo (lectura y mapeo)
                        using (var reader = new StreamReader(ArchivoCarga.OpenReadStream(), Encoding.UTF8))
                        {
                            string line;
                            int lineNumber = 0;
                            while ((line = await reader.ReadLineAsync()) != null)
                            {
                                lineNumber++;
                                if (lineNumber == 1) continue;
                                if (string.IsNullOrWhiteSpace(line)) continue;

                                registrosTotalesEnArchivo++;

                                try
                                {
                                    var campos = line.Split('\t');

                                    // 🎯 CORRECCIÓN: Lógica de validación de campos movida DENTRO del IF de TipoArchivo
                                    if (TipoArchivo == "Compras")
                                    {
                                        if (campos.Length < 11)
                                        {
                                            erroresLote.Add($"Línea {lineNumber}: Campos insuficientes (Compras). Se esperaban 11, se encontraron {campos.Length}.");
                                            continue;
                                        }

                                        var nuevaCompra = MapearCompra(campos, archivoAnio, archivoMes);
                                        var claveLote = (nuevaCompra.NumComprobante, nuevaCompra.IdProveedor);

                                        if (!comprobantesEnLote.Add(claveLote))
                                        {
                                            erroresLote.Add($"Línea {lineNumber}: Duplicado en archivo.");
                                            continue;
                                        }

                                        nuevaCompra.CargaLoteId = loteIdReal;
                                        nuevasCompras.Add(nuevaCompra);
                                        registrosValidosParaInsercion++;
                                    }
                                    else if (TipoArchivo == "Retenciones")
                                    {
                                        if (campos.Length < 11)
                                        { // Retenciones usa el mismo formato de 12 campos
                                            erroresLote.Add($"Línea {lineNumber}: Campos insuficientes (Retenciones). Se esperaban 11, se encontraron {campos.Length}.");
                                            continue;
                                        }

                                        var (ventaResultado, resultadoMensaje) = await ProcesarRetencionRegistroAsync(campos, archivoAnio, archivoMes, loteIdReal);
                                        if (ventaResultado != null)
                                        {
                                            ventasCargadas.Add(ventaResultado);
                                            registrosValidosParaInsercion++;
                                            if (resultadoMensaje == "ACTUALIZADA") retencionesActualizadas++;
                                            else if (resultadoMensaje == "CREADA") ventasCeroCreadas++;
                                        }
                                        else
                                        {
                                            erroresLote.Add($"Línea {lineNumber}: {resultadoMensaje}");
                                        }
                                    }
                                    // 🎯 CORRECCIÓN: Lógica de carga de Ventas (8 campos)
                                    else if (TipoArchivo == "Ventas")
                                    {
                                        if (campos.Length < 8)
                                        {
                                            erroresLote.Add($"Línea {lineNumber}: Campos insuficientes (Ventas). Se esperaban 8, se encontraron {campos.Length}.");
                                            continue;
                                        }

                                        var nuevaVenta = MapearVenta(campos, loteIdReal); // <--- NUEVO MÉTODO
                                        ventasCargadas.Add(nuevaVenta);
                                        registrosValidosParaInsercion++;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    erroresLote.Add($"Línea {lineNumber}: Error de formato/conversión. Detalle: {ex.Message}");
                                }
                            } // Fin While
                        } // Fin Using Reader

                        // 3. Guardar Hijos en la DB
                        if (TipoArchivo == "Compras")
                        {
                            _db.Compras.AddRange(nuevasCompras);
                        }
                        else if (TipoArchivo == "Retenciones" || TipoArchivo == "Ventas")
                        {
                            foreach (var venta in ventasCargadas)
                            {
                                if (venta.Id > 0 && TipoArchivo == "Retenciones") // Solo Retenciones actualiza
                                {
                                    _db.Ventas.Attach(venta);
                                    _db.Entry(venta).Property(v => v.valRetIVA).IsModified = true;
                                    _db.Entry(venta).Property(v => v.valRetRenta).IsModified = true;
                                    _db.Entry(venta).Property(v => v.NumRetencion).IsModified = true;
                                    _db.Entry(venta).Property(v => v.FechaRetencion).IsModified = true;
                                    _db.Entry(venta).Property(v => v.AutorizacionRetencion).IsModified = true;
                                    _db.Entry(venta).Property(v => v.CargaLoteId).IsModified = true;
                                }
                                else // Ventas nuevas o Ventas Cero de Retenciones
                                {
                                    _db.Ventas.Add(venta);
                                }
                            }
                        }

                        // 4. Actualizar Total de Registros en el Lote Padre
                        nuevoLote.TotalRegistros = registrosValidosParaInsercion;
                        _db.CargasLotes.Update(nuevoLote);

                        // 5. Guardar Hijos y Lote (Segundo SaveChanges)
                        await _db.SaveChangesAsync();
                        await transaction.CommitAsync();

                    } // Fin Using Transaction
                    catch (DbUpdateException dbEx)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError(dbEx, "Error de BD al guardar lote de {TipoArchivo}.", TipoArchivo);
                        // 🎯 CORRECCIÓN: Mostrar el InnerException para ver el error de truncamiento
                        MensajeCarga = $"❌ **ERROR CRÍTICO DE BD (Registros Hijos):** No se pudieron guardar los detalles. Detalle: {dbEx.InnerException?.Message ?? dbEx.Message}";

                        if (nuevoLote != null && nuevoLote.Id > 0)
                        {
                            _db.CargasLotes.Remove(nuevoLote);
                            await _db.SaveChangesAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, "Error fatal en la transacción de carga para {TipoArchivo}.", TipoArchivo);
                        MensajeCarga = $"❌ **ERROR CRÍTICO:** {ex.Message}";
                    }
                } // Fin Using Strategy
            }); // Fin Strategy Execute

            // 6. Mensajes Finales
            if (string.IsNullOrEmpty(MensajeCarga)) // Si no hubo errores
            {
                if (TipoArchivo == "Compras")
                {
                    MensajeCarga = $"✅ **¡Carga Exitosa!** Se procesaron {registrosTotalesEnArchivo} registros de Compra. Se insertaron **{registrosValidosParaInsercion}** registros válidos.";
                }
                else if (TipoArchivo == "Retenciones")
                {
                    MensajeCarga = $"✅ **¡Carga Exitosa!** Se procesaron {registrosTotalesEnArchivo} registros de Retención. Se crearon **{ventasCeroCreadas}** facturas de Venta Cero y se actualizaron **{retencionesActualizadas}** ventas existentes.";
                }
                else if (TipoArchivo == "Ventas")
                {
                    MensajeCarga = $"✅ **¡Carga Exitosa!** Se procesaron {registrosTotalesEnArchivo} registros de Venta. Se insertaron **{registrosValidosParaInsercion}** registros válidos.";
                }

                if (erroresLote.Any())
                {
                    MensajeCarga += $" ⚠️ Hubo **{erroresLote.Count}** errores de formato/validación.";
                    ErroresValidacion = erroresLote;
                }
            }

            await OnGetAsync();
            return Page();
        }

        // --------------------------------------------------------------------
        // MÉTODOS AUXILIARES
        // --------------------------------------------------------------------

        private Compra MapearCompra(string[] campos, int anio, int mes)
        {
            // (El código de MapearCompra que ya teníamos)
            string numComprobanteCompleto = campos[3].Trim();
            string[] partesComprobante = numComprobanteCompleto.Split('-');
            string estab = partesComprobante.Length > 0 ? partesComprobante[0].Trim() : string.Empty;
            string ptoEmi = partesComprobante.Length > 1 ? partesComprobante[1].Trim() : string.Empty;
            string secuencial = partesComprobante.Length > 2 ? partesComprobante[2].Trim() : string.Empty;
            string numComprobanteLimpio = estab + ptoEmi + secuencial;

            string valorSinImpuestosStr = campos[8].Trim().Replace(',', '.');
            string montoIvaStr = campos[9].Trim().Replace(',', '.');
            string montoTotalStr = campos[10].Trim().Replace(',', '.');

            decimal valorSinImpuestos = decimal.Parse(valorSinImpuestosStr, CultureInfo.InvariantCulture);
            decimal montoIva = decimal.Parse(montoIvaStr, CultureInfo.InvariantCulture);

            // 🎯 CORRECCIÓN SEMÁNTICA (BASE IMPONIBLE vs GRAVADA)
            decimal baseGravaIva = (montoIva > 0) ? valorSinImpuestos : 0.00M;
            decimal baseNoGravaIva = (montoIva == 0) ? valorSinImpuestos : 0.00M;

            string codSustento = montoIva > 0 ? "01" : "02";
            DateTime fechaEmision = DateTime.ParseExact(
                campos[6].Trim(),
                "dd/MM/yyyy",
                System.Globalization.CultureInfo.InvariantCulture
            );
            string tipoIdProv;
            if (campos[0].Length == 13 && campos[0].EndsWith("001"))
            {
                tipoIdProv = "01";
            }
            else if (campos[0].Length == 10)
            {
                tipoIdProv = "02";
            }
            else
            {
                tipoIdProv = "03";
            }
            string codigoCompra = $"C{DateTime.Now.Ticks % 100000}";
            return new Compra
            {
                CodigoCompra = codigoCompra,
                Anio = (short)fechaEmision.Year,
                Mes = (short)fechaEmision.Month,
                CodSustento = codSustento,
                TipoIdProveedor = tipoIdProv,
                IdProveedor = campos[0].Trim(),
                RazonSocialProveedor = campos[1].Trim(),
                TipoComprobante = "01",
                ParteRelacionada = false,
                FechaRegistro = fechaEmision,
                FechaEmision = fechaEmision,
                Estab = estab,
                PtoEmi = ptoEmi,
                Secuencial = secuencial,
                NumComprobante = numComprobanteLimpio, // Usamos el número limpio
                Autorizacion = campos[4].Trim(),

                // 🎯 ASIGNACIÓN CORREGIDA
                BaseImponible = valorSinImpuestos, // El subtotal total (campo[8])
                BaseImpGrav = baseGravaIva,      // La parte gravada (si iva > 0)
                BaseNoGraIva = baseNoGravaIva,   // La parte no gravada (si iva == 0)

                BaseImpExe = 0.00M,
                MontoIva = montoIva,
                MontoIce = 0.00M,
                MontoTotal = decimal.Parse(montoTotalStr, CultureInfo.InvariantCulture),
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
                TipoRegi = string.Empty,
                PaisEfecPagoGen = string.Empty,
                PaisEfecPagoParFis = string.Empty,
                DenopagoRegFis = string.Empty,
                PaisEfecPago = string.Empty,
                AplicConvDobTrib = "NO",
                PagExtSujRetNorLeg = "NO",
                FormaPago = "20",
                UsuarioCreacion = "CargaMasiva",
                FechaCreacion = DateTime.Now
            };
        }

        // --------------------------------------------------------------------
        // 🎯 NUEVO MÉTODO AUXILIAR PARA MAPEAR VENTAS (Implementado)
        // --------------------------------------------------------------------
        private Venta MapearVenta(string[] campos, int loteIdReal)
        {
            // El archivo de Ventas tiene 8 campos relevantes
            // 0=COMPROBANTE, 1=SERIE_COMPROBANTE, 2=CLAVE_ACCESO, 3=FECHA_AUTORIZACION, 
            // 4=FECHA_EMISION, 5=VALOR_SIN_IMPUESTOS, 6=IVA, 7=IMPORTE_TOTAL

            // 🎯 SOLUCIÓN AL ERROR DE TRUNCAMIENTO: Mapear "Factura" a "01"
            string tipoComprobanteTexto = campos[0].Trim();
            string tipoComprobanteCodigo = "18"; // Default para Venta
            if (tipoComprobanteTexto.Equals("Factura", StringComparison.OrdinalIgnoreCase))
            {
                tipoComprobanteCodigo = "01"; // Código SRI para Factura (si su DB usa 01 para Ventas)
            }
            // Ajuste '18' o '01' según su catálogo interno en la DB (Venta.TipoComprobante)
            // Usaré '01' basado en su error anterior "Truncated value: 'Fa'".

            var numComprobanteCompleto = campos[1]; // SERIE_COMPROBANTE (Ej: 001-002-000000900)
            var claveAcceso = campos[2]; // CLAVE_ACCESO

            if (!DateTime.TryParseExact(campos[4].Split(' ')[0], "dd/MM/yyyy", _loadCulture, DateTimeStyles.None, out DateTime fechaEmision))
            {
                throw new FormatException($"Formato de fecha inválido: {campos[4]}");
            }

            if (!decimal.TryParse(campos[5].Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal valorSinImpuestos) ||
                !decimal.TryParse(campos[6].Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal montoIva) ||
                !decimal.TryParse(campos[7].Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal montoTotal))
            {
                throw new FormatException($"Formato numérico inválido: {campos[5]}/{campos[6]}/{campos[7]}");
            }

            decimal baseGravaIva = (montoIva > 0) ? valorSinImpuestos : 0.00m;
            decimal baseNoGravaIva = (montoIva == 0) ? valorSinImpuestos : 0.00m;

            string[] partesComprobante = numComprobanteCompleto.Split('-');
            string estab = partesComprobante.Length > 0 ? partesComprobante[0].Trim() : string.Empty;
            string ptoEmi = partesComprobante.Length > 1 ? partesComprobante[1].Trim() : string.Empty;
            string secuencial = partesComprobante.Length > 2 ? partesComprobante[2].Trim() : string.Empty;
            string numComprobanteLimpio = estab + ptoEmi + secuencial;

            var nuevaVenta = new Venta
            {
                CargaLoteId = loteIdReal,
                TipoComprobante = tipoComprobanteCodigo, // 🎯 CORREGIDO: Usar "01" en lugar de "Factura"
                NumComprobante = numComprobanteLimpio, // Guardamos la serie SIN guiones

                Estab = estab,
                PtoEmi = ptoEmi,
                Secuencial = secuencial,

                ClaveAcceso = claveAcceso,
                FechaEmision = fechaEmision,
                Anio = (short)fechaEmision.Year,
                Mes = (short)fechaEmision.Month,

                BaseImponible = valorSinImpuestos,
                BaseImpGrav = baseGravaIva,
                BaseNoGraIva = baseNoGravaIva,

                MontoIva = montoIva,
                MontoTotal = montoTotal,

                IdCliente = claveAcceso.Length >= 23 ? claveAcceso.Substring(10, 13) : "9999999999999",
                RazonSocialCliente = "Cliente No Identificado (Carga Lote)",

                valRetIVA = 0.00M,
                valRetRenta = 0.00M,
                NumRetencion = string.Empty,
                FormaPago = "20",
                FechaCreacion = DateTime.Now,
                UsuarioCreacion = "CargaMasiva"
            };

            return nuevaVenta;
        }

        // ... (El método ProcesarRetencionRegistroAsync se mantiene) ...
        private async Task<(Venta? venta, string mensaje)> ProcesarRetencionRegistroAsync(string[] campos, int anio, int mes, int cargaLoteId)
        {
            decimal valorRetenidoRentaReal = 0.00m;
            decimal valorRetenidoIVAReal = 0.00m;
            const decimal TASA_RETENCION_RENTA = 0.0275m;
            const decimal TASA_RETENCION_IVA = 0.70m;
            string rucAgenteRetencion = campos[0].Trim();
            string razonSocialAgente = campos[1].Trim();
            string numComprobanteVenta = campos[11].Trim();
            if (string.IsNullOrEmpty(numComprobanteVenta))
            {
                return (null, "ERROR: El Número de Comprobante de Venta está vacío en el archivo de retenciones.");
            }
            if (!DateTime.TryParseExact(campos[6].Trim(), "dd/MM/yyyy", _loadCulture, DateTimeStyles.None, out DateTime fechaEmisionRetencion))
            {
                return (null, "ERROR: Formato de fecha de emisión de retención inválido.");
            }

            string numComprobanteVentaLimpio = numComprobanteVenta.Replace("-", "");

            var ventaAActualizar = await _db.Ventas
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.NumComprobante == numComprobanteVentaLimpio);

            if (ventaAActualizar != null)
            {
                if (ventaAActualizar.valRetRenta == 0.00m && ventaAActualizar.BaseImponible > 0)
                {
                    ventaAActualizar.valRetRenta = ventaAActualizar.BaseImponible * TASA_RETENCION_RENTA;
                }
                if (ventaAActualizar.valRetIVA == 0.00m && ventaAActualizar.MontoIva > 0)
                {
                    ventaAActualizar.valRetIVA = ventaAActualizar.MontoIva * TASA_RETENCION_IVA;
                }

                string numRetencionCompleto = campos[3].Trim();
                string[] partesRetencion = numRetencionCompleto.Split('-');
                string estabRet = partesRetencion.Length > 0 ? partesRetencion[0].Trim() : string.Empty;
                string ptoEmiRet = partesRetencion.Length > 1 ? partesRetencion[1].Trim() : string.Empty;
                string secRet = partesRetencion.Length > 2 ? partesRetencion[2].Trim() : string.Empty;

                ventaAActualizar.NumRetencion = numRetencionCompleto.Replace("-", "");
                ventaAActualizar.Estab = estabRet;
                ventaAActualizar.PtoEmi = ptoEmiRet;
                ventaAActualizar.Secuencial = secRet;

                ventaAActualizar.FechaRetencion = fechaEmisionRetencion;
                ventaAActualizar.AutorizacionRetencion = campos[4].Trim();
                ventaAActualizar.CargaLoteId = cargaLoteId;
                return (ventaAActualizar, "ACTUALIZADA");
            }
            else
            {
                // 🎯 LÓGICA DE VENTA CERO CORREGIDA
                string numRetencionCompleto = campos[3].Trim();
                string[] partesRetencion = numRetencionCompleto.Split('-');
                string estabRet = partesRetencion.Length > 0 ? partesRetencion[0].Trim() : string.Empty;
                string ptoEmiRet = partesRetencion.Length > 1 ? partesRetencion[1].Trim() : string.Empty;
                string secRet = partesRetencion.Length > 2 ? partesRetencion[2].Trim() : string.Empty;

                var nuevaVentaCero = new Venta
                {
                    Anio = (short)anio,
                    Mes = (short)mes,
                    CargaLoteId = cargaLoteId,
                    TipoComprobante = "18", // 18 = Venta (Factura)
                    NumComprobante = numComprobanteVentaLimpio,
                    IdCliente = rucAgenteRetencion,
                    RazonSocialCliente = razonSocialAgente,

                    BaseImponible = 0,
                    BaseImpGrav = 0,
                    BaseNoGraIva = 0,
                    MontoIva = 0,
                    MontoTotal = 0,

                    valRetIVA = 0.01M, // Placeholder
                    valRetRenta = 0.01M, // Placeholder
                    NumRetencion = numRetencionCompleto.Replace("-", ""),
                    AutorizacionRetencion = campos[4].Trim(),
                    FechaRetencion = fechaEmisionRetencion,
                    Estab = estabRet,
                    PtoEmi = ptoEmiRet,
                    Secuencial = secRet,

                    UsuarioCreacion = "CargaMasivaRet",
                    FechaCreacion = DateTime.Now,
                    FormaPago = "20"
                };
                return (nuevaVentaCero, "CREADA");
            }
        }

        // ===================================================================
        // OnPostEliminarAsync (Corregido y Robusto)
        // ===================================================================
        public async Task<IActionResult> OnPostDeleteLoteAsync(int id)
        {
            if (id <= 0)
            {
                MensajeCarga = "❌ **ERROR CRÍTICO:** ID de lote inválido para eliminación.";
                await OnGetAsync(); // Recargar la página para mostrar el error
                return Page();
            }

            try
            {
                var lote = await _db.CargasLotes.FindAsync(id);

                if (lote == null)
                {
                    MensajeCarga = $"❌ **ERROR CRÍTICO:** Lote con ID {id} no encontrado.";
                    await OnGetAsync();
                    return Page();
                }

                string tipoArchivo = lote.TipoArchivo; // Usamos el tipo del lote guardado

                long registrosReseteados = 0;
                int registrosEliminadosVentaCero = 0;
                const decimal zeroAmount = 0.00m;

                var strategy = _db.Database.CreateExecutionStrategy();
                await strategy.Execute(async () =>
                {
                    using (IDbContextTransaction transaction = await _db.Database.BeginTransactionAsync())
                    {
                        try
                        {
                            if (tipoArchivo == "Compras")
                            {
                                await _db.Compras
                                   .Where(c => c.CargaLoteId == id)
                                   .ExecuteDeleteAsync();
                            }
                            else if (tipoArchivo == "Retenciones")
                            {
                                // ... (La lógica de Retenciones que ya funcionaba) ...
                                var loteIdParam = new SqlParameter("@p0", SqlDbType.Int) { Value = id };
                                var zeroAmountParam = new SqlParameter("@p1", SqlDbType.Decimal)
                                {
                                    Value = zeroAmount,
                                    Precision = 18,
                                    Scale = 2
                                };

                                registrosReseteados = await _db.Ventas.AsNoTracking()
                                   .Where(v => v.CargaLoteId == id && v.MontoTotal > zeroAmount)
                                   .LongCountAsync();
                                registrosEliminadosVentaCero = await _db.Ventas.AsNoTracking()
                                   .Where(v => v.CargaLoteId == id && v.MontoTotal == zeroAmount)
                                   .CountAsync();

                                await _db.Database.ExecuteSqlRawAsync(
                                    @"UPDATE dbo.Ventas
                                SET 
                                    CargaLoteId = NULL,         
                                    valRetIVA = 0.00,
                                    valRetRenta = 0.00,
                                    NumRetencion = NULL,
                                    FechaRetencion = NULL,
                                    AutorizacionRetencion = NULL
                                WHERE 
                                    CargaLoteId = @p0 AND MontoTotal > @p1",
                                    new[] { loteIdParam, zeroAmountParam });

                                loteIdParam.Value = id;
                                zeroAmountParam.Value = zeroAmount;

                                await _db.Database.ExecuteSqlRawAsync(
                                    @"DELETE FROM dbo.Ventas
                                    WHERE CargaLoteId = @p0 AND MontoTotal = @p1",
                                    new[] { loteIdParam, zeroAmountParam });
                            }
                            else if (tipoArchivo == "Ventas")
                            {
                                // 🎯 CORRECCIÓN PARA VENTAS (ELIMINACIÓN DE NIETOS)

                                // 1. Obtener los IDs de las Ventas (hijos) que se van a eliminar
                                var ventasParaEliminarIds = await _db.Ventas
                                    .Where(v => v.CargaLoteId == id)
                                    .Select(v => v.Id)
                                    .ToListAsync();

                                if (ventasParaEliminarIds.Any())
                                {
                                    // 2. Eliminar los "Nietos" (ej. VentaDetalles)
                                    // ⚠️ REEMPLACE 'VentaDetalles' Y 'VentaId' CON LOS NOMBRES DE SU DB
                                    // await _db.VentaDetalles 
                                    //    .Where(d => ventasParaEliminarIds.Contains(d.VentaId))
                                    //    .ExecuteDeleteAsync();
                                }

                                // 3. Eliminar los "Hijos" (Ventas)
                                await _db.Ventas
                                    .Where(v => v.CargaLoteId == id)
                                    .ExecuteDeleteAsync();
                            }

                            // 4. Eliminar el "Padre" (Lote)
                            _db.CargasLotes.Remove(lote);
                            await _db.SaveChangesAsync();

                            await transaction.CommitAsync();

                            // ... (Mensajes de éxito) ...
                        }
                        catch (Exception ex)
                        {
                            await transaction.RollbackAsync();
                            _logger.LogError(ex, "Error al eliminar lote con ID {Id} y tipo {TipoArchivo}.", id, tipoArchivo);
                            throw;
                        }
                    }
                }); // Fin de strategy.Execute
            }
            catch (Exception ex)
            {
                MensajeCarga = $"❌ **ERROR CRÍTICO AL ELIMINAR LOTE:** No se pudo completar la operación. Detalle: {ex.InnerException?.Message ?? ex.Message}";
            }

            await OnGetAsync();
            return Page();
        }
    }
}