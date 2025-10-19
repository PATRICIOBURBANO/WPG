using AtsManager.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient; // CLAVE para Raw SQL Parameters
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data; // 

namespace AtsManager.Pages.Cargas
{
    public class IndexModel : PageModel
    {
        private readonly AtsDbContext _db;
        private readonly CultureInfo _loadCulture = new CultureInfo("es-EC");

        // Propiedades para el Formulario de Carga
        [BindProperty]
        public IFormFile ArchivoCarga { get; set; } = default!;

        [BindProperty]
        public int Anio { get; set; } = DateTime.Now.Year;

        [BindProperty]
        public int Mes { get; set; } = DateTime.Now.Month;

        [BindProperty]
        public string TipoArchivo { get; set; } = string.Empty;

        // Propiedad para la confirmación de reemplazo de lote
        [BindProperty]
        public bool ConfirmarReemplazo { get; set; }

        public string MensajeCarga { get; set; } = string.Empty;
        public List<string> ErroresValidacion { get; set; } = new List<string>();

        public IList<CargaLote> CargasLotes { get; set; } = new List<CargaLote>();

        public IndexModel(AtsDbContext context)
        {
            _db = context;
        }

        public async Task OnGetAsync()
        {
            CargasLotes = await _db.CargasLotes
             .OrderByDescending(c => c.Anio)
             .ThenByDescending(c => c.Mes)
             .ThenByDescending(c => c.FechaCarga)
             .ToListAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await OnGetAsync();

            if (ArchivoCarga == null || ArchivoCarga.Length == 0 || string.IsNullOrEmpty(TipoArchivo))
            {
                MensajeCarga = "Debe seleccionar un archivo y un tipo de anexo.";
                return Page();
            }

            // ====================================================================
            // PASO 1. PRE-VALIDACIÓN DE CONSISTENCIA DE PERÍODO (LÓGICA CORRECTA)
            // ====================================================================

            ArchivoCarga.OpenReadStream().Seek(0, SeekOrigin.Begin);

            using (var reader = new StreamReader(ArchivoCarga.OpenReadStream(), Encoding.UTF8))
            {
                string line;
                int lineNumber = 0;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    lineNumber++;
                    if (lineNumber == 1) continue;

                    try
                    {
                        var campos = line.Split('\t');
                        if (campos.Length < 11) continue;

                        if (DateTime.TryParseExact(campos[6].Trim(), "dd/MM/yyyy", _loadCulture, DateTimeStyles.None, out DateTime fecha))
                        {
                            if (fecha.Year != Anio || fecha.Month != Mes)
                            {
                                MensajeCarga = $"❌ **ERROR CRÍTICO:** Carga Cancelada. La línea {lineNumber} tiene fecha **{fecha.ToString("MM/yyyy")}**, que no coincide con el período de carga seleccionado **{Mes}/{Anio}**.";
                                return Page();
                            }
                        }
                        else
                        {
                            MensajeCarga = $"❌ **ERROR CRÍTICO:** Carga Cancelada. Formato de fecha inválido en la línea {lineNumber} (Valor: {campos[6]}). Formato esperado: dd/MM/yyyy.";
                            return Page();
                        }
                    }
                    catch (Exception ex)
                    {
                        MensajeCarga = $"❌ **ERROR CRÍTICO:** Carga Cancelada. Error inesperado en la línea {lineNumber}: {ex.Message}";
                        return Page();
                    }
                }
            }


            // ====================================================================
            // PASO 2. COMPROBACIÓN DE LOTE EXISTENTE Y SOLICITUD DE CONFIRMACIÓN (LÓGICA CORRECTA)
            // ====================================================================
            CargaLote loteEliminado = null;
            if (TipoArchivo == "Compras" || TipoArchivo == "Retenciones")
            {
                var loteExistente = await _db.CargasLotes.FirstOrDefaultAsync(c =>
                 c.Anio == Anio && c.Mes == Mes && c.TipoArchivo == TipoArchivo);

                // Condición 1: Lote existe y NO se ha confirmado el reemplazo
                if (loteExistente != null && !ConfirmarReemplazo)
                {
                    // Guardar los datos de carga para el POST de confirmación
                    TempData["Anio"] = Anio;
                    TempData["Mes"] = Mes;
                    TempData["TipoArchivo"] = TipoArchivo;
                    TempData["ArchivoCargaNombre"] = ArchivoCarga.FileName;
                    TempData["MensajeConfirmacion"] = $"⚠️ ¡ATENCIÓN! Ya existe una carga de **{TipoArchivo}** para el período **{Mes}/{Anio}** (Archivo: {loteExistente.NombreArchivo}). ¿Desea **eliminar el lote existente** y reemplazarlo?";
                    return Page();
                }

                // Condición 2: Lote existe y SÍ se confirmó el reemplazo (Eliminar)
                if (loteExistente != null && ConfirmarReemplazo)
                {
                    loteEliminado = loteExistente;
                    int loteId = loteExistente.Id;

                    try
                    {
                        // Eliminación Masiva Optimizada
                        if (TipoArchivo == "Compras")
                        {
                            await _db.Compras
                             .Where(c => c.CargaLoteId == loteId)
                             .ExecuteDeleteAsync();
                        }

                        // Lógica de eliminación de Retenciones ya existente (Correcta: Resetea > 0 y Elimina = 0)
                        if (TipoArchivo == "Retenciones")
                        {
                            // 1. Eliminar Ventas Cero
                            await _db.Ventas
       .Where(v => v.CargaLoteId == loteId && v.MontoTotal == 0.00m)
       .ExecuteDeleteAsync();

                            // 2. Resetear CargaLoteId (Desvincular) para ventas con MontoTotal > 0
                            await _db.Ventas
       .Where(v => v.CargaLoteId == loteId && v.MontoTotal > 0.00m)
       .ExecuteUpdateAsync(setter => setter
        .SetProperty(v => v.CargaLoteId, (int?)null)
        .SetProperty(v => v.valRetIVA, 0.00M)
        .SetProperty(v => v.valRetRenta, 0.00M)
        .SetProperty(v => v.NumRetencion, (string?)null)
        .SetProperty(v => v.FechaRetencion, (DateTime?)null)
        .SetProperty(v => v.AutorizacionRetencion, (string?)null)
       );
                        }

                        // Se elimina el lote padre después de limpiar los hijos
                        _db.CargasLotes.Remove(loteExistente);
                        await _db.SaveChangesAsync(); // Guardar cambios de limpieza y eliminación de lote
                    }
                    catch (DbUpdateException ex)
                    {
                        MensajeCarga = $"❌ **ERROR CRÍTICO DE BD (Eliminación de Lote Existente):** Falló al eliminar el lote existente. Detalle: {ex.InnerException?.Message}";
                        await OnGetAsync();
                        return Page();
                    }

                    MensajeCarga = $"⚠️ Lote existente ({loteId}) ELIMINADO. Procesando nueva carga...";
                }
            }

            // ====================================================================
            // PASO 3. PROCESAMIENTO, MAPEO Y VALIDACIÓN DE DUPLICADOS (LÓGICA CORREGIDA)
            // ====================================================================

            var nuevoLote = new CargaLote
            {
                Anio = Anio,
                Mes = Mes,
                TipoArchivo = TipoArchivo,
                NombreArchivo = ArchivoCarga.FileName,
                FechaCarga = DateTime.Now
            };

            int registrosTotalesEnArchivo = 0;
            int registrosValidosParaInsercion = 0;
            int registrosDuplicadosExistentes = 0;
            int retencionesActualizadas = 0;
            int ventasCeroCreadas = 0;

            List<string> erroresLote = new List<string>();
            List<Compra> nuevasCompras = new List<Compra>();
            List<Venta> ventasCargadas = new List<Venta>();

            ArchivoCarga.OpenReadStream().Seek(0, SeekOrigin.Begin);
            HashSet<(string NumComprobante, string IdProveedor)> comprobantesEnLote = new HashSet<(string, string)>();

            using (var reader = new StreamReader(ArchivoCarga.OpenReadStream(), Encoding.UTF8))
            {
                string line;
                int lineNumber = 0;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    lineNumber++;
                    if (lineNumber == 1) continue;

                    registrosTotalesEnArchivo++;

                    try
                    {
                        var campos = line.Split('\t');
                        if (campos.Length < 11)
                        {
                            erroresLote.Add($"Línea {lineNumber}: Faltan campos. Se esperaban 12, se encontraron {campos.Length}.");
                            continue;
                        }

                        if (TipoArchivo == "Compras")
                        {
                            // === LÓGICA DE COMPRAS ===
                            var nuevaCompra = MapearCompra(campos, Anio, Mes);

                            var claveLote = (nuevaCompra.NumComprobante, nuevaCompra.IdProveedor);
                            if (!comprobantesEnLote.Add(claveLote))
                            {
                                erroresLote.Add($"Línea {lineNumber}: Registro duplicado en el archivo (Comprobante {claveLote.NumComprobante} / Proveedor {claveLote.IdProveedor}).");
                                continue;
                            }

                            var duplicadoDb = await _db.Compras.AsNoTracking().FirstOrDefaultAsync(c =>
                               c.NumComprobante == nuevaCompra.NumComprobante &&
                               c.IdProveedor == nuevaCompra.IdProveedor);

                            if (duplicadoDb != null && (loteEliminado == null || duplicadoDb.CargaLoteId != loteEliminado.Id))
                            {
                                registrosDuplicadosExistentes++;
                                erroresLote.Add($"Línea {lineNumber}: El registro ya existe en otro lote.");
                                continue;
                            }

                            // nuevaCompra.CargaLoteId = nuevoLote.Id; // ELIMINADO: Se asignará con el ID real después del SaveChanges.
                            nuevasCompras.Add(nuevaCompra);
                            registrosValidosParaInsercion++;
                        }
                        else if (TipoArchivo == "Retenciones")
                        {
                            // === LÓGICA DE RETENCIONES ===
                            // Se mantiene como estaba, ya que la lógica de retenciones ya gestiona Add/Attach.
                            var (ventaResultado, resultadoMensaje) = await ProcesarRetencionRegistroAsync(campos, Anio, Mes, nuevoLote.Id);

                            if (ventaResultado == null)
                            {
                                erroresLote.Add($"Línea {lineNumber}: {resultadoMensaje}");
                            }
                            else
                            {
                                // Devolvemos la entidad que será guardada/actualizada
                                ventasCargadas.Add(ventaResultado);
                                registrosValidosParaInsercion++;

                                if (resultadoMensaje == "ACTUALIZADA")
                                    retencionesActualizadas++;
                                else if (resultadoMensaje == "CREADA")
                                    ventasCeroCreadas++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        erroresLote.Add($"Línea {lineNumber}: Error de formato/conversión. Detalle: {ex.Message}");
                    }
                }
            }

            // ====================================================================
            // PASO 4. DECISIÓN DE GUARDADO (Guardar Cambios)
            // ====================================================================

            if ((TipoArchivo == "Compras" && nuevasCompras.Any()) || (TipoArchivo == "Retenciones" && registrosValidosParaInsercion > 0))
            {
                // 1. Añadir el lote principal (Padre) al contexto.
                nuevoLote.TotalRegistros = registrosValidosParaInsercion;
                _db.CargasLotes.Add(nuevoLote);

                // 2. PRIMER GUARDADO: Guardar SOLO CargasLotes para obtener el Id real.
                try
                {
                    await _db.SaveChangesAsync();
                }
                catch (DbUpdateException ex)
                {
                    MensajeCarga = $"❌ **ERROR CRÍTICO DE BD:** No se pudo guardar el lote padre. Detalle: {ex.InnerException?.Message}";
                    await OnGetAsync();
                    return Page();
                }

                // El nuevoLote.Id ahora contiene el ID real de la base de datos.
                int loteIdReal = nuevoLote.Id;
                int registrosGuardados = 0;

                // 3. Reconectar y guardar los registros hijos.
                if (TipoArchivo == "Compras")
                {
                    foreach (var compra in nuevasCompras)
                    {
                        compra.CargaLoteId = loteIdReal;
                        // 🎯 CAMBIO CRÍTICO: Usar Add individual para asegurar el seguimiento de propiedades decimal? = 0.00M
                        _db.Compras.Add(compra);
                    }
                    // _db.Compras.AddRange(nuevasCompras); // ELIMINADO
                    registrosGuardados = nuevasCompras.Count;
                }
                else if (TipoArchivo == "Retenciones")
                {
                    foreach (var venta in ventasCargadas)
                    {
                        // 3b. Asignar el ID real
                        venta.CargaLoteId = loteIdReal;

                        // 3c. Determinar si se actualiza o se añade
                        if (venta.Id > 0)
                        {
                            // 💥 CORRECCIÓN CRÍTICA: La entidad ya es NO-Tracking. Attach y marcar.
                            // Esto resuelve el error "Cannot insert the value NULL into column 'NumComprobante'"
                            _db.Ventas.Attach(venta);

                            // Marcar solo los campos de Retención y CargaLoteId como modificados
                            _db.Entry(venta).Property(v => v.valRetIVA).IsModified = true;
                            _db.Entry(venta).Property(v => v.valRetRenta).IsModified = true;
                            _db.Entry(venta).Property(v => v.NumRetencion).IsModified = true;
                            _db.Entry(venta).Property(v => v.FechaRetencion).IsModified = true;
                            _db.Entry(venta).Property(v => v.AutorizacionRetencion).IsModified = true;
                            _db.Entry(venta).Property(v => v.CargaLoteId).IsModified = true; // Se debe asignar el lote real
                        }
                        else
                        {
                            // Venta Cero es nueva (Id = 0)
                            _db.Ventas.Add(venta);
                        }
                        registrosGuardados++;
                    }
                }

                // 4. SEGUNDO GUARDADO: Guardar los registros hijos (Compras o Ventas).
                if (registrosGuardados > 0)
                {
                    try
                    {
                        await _db.SaveChangesAsync();
                    }
                    catch (DbUpdateException ex)
                    {
                        // IMPORTANTE: Si falla el segundo save, elimine el lote padre creado.
                        _db.CargasLotes.Remove(nuevoLote);
                        await _db.SaveChangesAsync();
                        MensajeCarga = $"❌ **ERROR CRÍTICO DE BD (Registros Hijos):** No se pudieron guardar los detalles. Posible Duplicado o FK no válida. Detalle: {ex.InnerException?.Message}";
                        return Page();
                    }
                }
            }

            await OnGetAsync();

            // ====================================================================
            // PASO 5. MENSAJES FINALES (ÉXITO/ADVERTENCIA) (LÓGICA CORRECTA)
            // ====================================================================
            if (TipoArchivo == "Compras")
            {
                string msg = $"✅ **¡Carga Exitosa!** Se procesaron {registrosTotalesEnArchivo} registros de Compra. ";
                msg += $"Se insertaron **{registrosValidosParaInsercion}** registros válidos. ";

                if (registrosDuplicadosExistentes > 0)
                {
                    msg += $"⚠️ Se omitieron **{registrosDuplicadosExistentes}** registros duplicados ya existentes.";
                }

                if (erroresLote.Any())
                {
                    msg += $" ⚠️ Hubo **{erroresLote.Count}** errores de formato/validación.";
                    ErroresValidacion = erroresLote;
                }
                MensajeCarga = msg;
            }
            else if (TipoArchivo == "Retenciones" && registrosTotalesEnArchivo > 0)
            {
                string msg = $"✅ **¡Carga Exitosa!** Se procesaron {registrosTotalesEnArchivo} registros de Retención. ";
                msg += $"Se crearon **{ventasCeroCreadas}** facturas de Venta Cero y se actualizaron **{retencionesActualizadas}** ventas existentes.";

                if (erroresLote.Any())
                {
                    msg += $" ⚠️ Hubo **{erroresLote.Count}** errores de formato/conversión.";
                    ErroresValidacion = erroresLote;
                }

                MensajeCarga = msg;
            }

            return Page();
        }

        // --------------------------------------------------------------------
        // MÉTODOS AUXILIARES
        // --------------------------------------------------------------------

        private Compra MapearCompra(string[] campos, int anio, int mes)
        {
            // Lógica de MapearCompra (se mantiene igual, no es la fuente del error)
            string valorSinImpuestosStr = campos[8].Trim().Replace('.', ',');
            if (valorSinImpuestosStr.StartsWith(",")) valorSinImpuestosStr = "0" + valorSinImpuestosStr;

            string montoIvaStr = campos[9].Trim().Replace('.', ',');
            if (montoIvaStr.StartsWith(",")) montoIvaStr = "0" + montoIvaStr;

            string montoTotalStr = campos[10].Trim().Replace('.', ',');
            if (montoTotalStr.StartsWith(",")) montoTotalStr = "0" + montoTotalStr;

            decimal valorSinImpuestos = decimal.Parse(valorSinImpuestosStr, _loadCulture);
            decimal montoIva = decimal.Parse(montoIvaStr, _loadCulture);

            decimal baseGravaIva = montoIva > 0 ? valorSinImpuestos : 0.00M;   // Grava IVA
            decimal baseNoGravaIva = montoIva == 0 ? valorSinImpuestos : 0.00M; // No grava IVA
            string codSustento = montoIva > 0 ? "01" : "02";

            DateTime fechaEmision = DateTime.ParseExact(
        campos[6].Trim(),
        "dd/MM/yyyy",
        System.Globalization.CultureInfo.InvariantCulture // Usar cultura invariante
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
            string numComprobanteLimpio = campos[3].Trim().Replace("-", "");

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

                NumComprobante = numComprobanteLimpio,
                Autorizacion = campos[4].Trim(),

                BaseImponible = baseGravaIva,
                BaseImpGrav = baseGravaIva,       // Ambas con valor grava IVA
                BaseNoGraIva = baseNoGravaIva,    // Esta con la base 0%
                BaseImpExe = 0.00M,
                MontoIva = montoIva,
                MontoIce = 0.00M,
                MontoTotal = decimal.Parse(montoTotalStr, _loadCulture),

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
                FormaPago = "01",
                TipoProveedor = "01",

                UsuarioCreacion = "CargaMasiva",
                FechaCreacion = DateTime.Now
                // CargaLoteId se asigna en OnPostAsync
            };
        }

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

            // BÚSQUEDA: Se mantiene AsNoTracking. La entidad será Attach/Add en OnPostAsync.
            var ventaAActualizar = await _db.Ventas
    .AsNoTracking()
    .FirstOrDefaultAsync(v => v.NumComprobante == numComprobanteVenta);

            if (ventaAActualizar != null)
            {
                // Llenar/Calcular valores de Retención
                if (ventaAActualizar.valRetRenta == 0.00m && ventaAActualizar.BaseImponible > 0)
                {
                    ventaAActualizar.valRetRenta = ventaAActualizar.BaseImponible * TASA_RETENCION_RENTA;
                }
                if (ventaAActualizar.valRetIVA == 0.00m && ventaAActualizar.MontoIva > 0)
                {
                    ventaAActualizar.valRetIVA = ventaAActualizar.MontoIva * TASA_RETENCION_IVA;
                }

                // Actualizar campos de Retención
                ventaAActualizar.NumRetencion = campos[3].Trim().Replace("-", "");
                ventaAActualizar.FechaRetencion = fechaEmisionRetencion;
                ventaAActualizar.AutorizacionRetencion = campos[4].Trim();
                ventaAActualizar.CargaLoteId = cargaLoteId; // Se asigna aquí para ser arrastrado

                return (ventaAActualizar, "ACTUALIZADA");
            }
            else
            {
                // Crear Venta Cero
                var nuevaVentaCero = new Venta
                {
                    Anio = (short)anio,
                    Mes = (short)mes,
                    CargaLoteId = cargaLoteId,

                    TipoComprobante = "18",
                    NumComprobante = numComprobanteVenta, // ✔️ CRÍTICO: Garantizado NOT NULL
                    FechaEmision = fechaEmisionRetencion,
                    IdCliente = rucAgenteRetencion,
                    RazonSocialCliente = razonSocialAgente,

                    MontoTotal = 0.00m,
                    BaseImponible = 0.00m,
                    MontoIva = 0.00m,
                    FormaPago = "20",

                    valRetRenta = valorRetenidoRentaReal,
                    valRetIVA = valorRetenidoIVAReal,
                    FechaRetencion = fechaEmisionRetencion,

                    NumRetencion = campos[3].Trim().Replace("-", ""),
                    AutorizacionRetencion = campos[4].Trim(),

                    UsuarioCreacion = "CargaRetencion",
                    FechaCreacion = DateTime.Now
                };

                return (nuevaVentaCero, "CREADA");
            }
        }


        // --- OnPostDeleteLoteAsync --- (Lógica de eliminación de lote Retenciones mejorada con Raw SQL)
        public async Task<IActionResult> OnPostDeleteLoteAsync(int id)
        {
            var lote = await _db.CargasLotes.FindAsync(id);
            if (lote == null)
            {
                MensajeCarga = "Error: Lote no encontrado.";
                await OnGetAsync();
                return Page();
            }

            int loteId = lote.Id;
            string tipoArchivo = lote.TipoArchivo;
            long registrosReseteados = 0;
            int registrosEliminadosVentaCero = 0;

            // Usamos el literal decimal '0.00m' para asegurar la precisión.
            const decimal zeroAmount = 0.00m;

            var strategy = _db.Database.CreateExecutionStrategy();

            try
            {
                await strategy.ExecuteAsync(async () =>
                {
                    using (IDbContextTransaction transaction = await _db.Database.BeginTransactionAsync())
                    {
                        try
                        {
                            if (tipoArchivo == "Compras")
                            {
                                await _db.Compras
                                   .Where(c => c.CargaLoteId == loteId)
                                   .ExecuteDeleteAsync();
                            }
                            else if (tipoArchivo == "Retenciones")
                            {
                                // 💥 SOLUCIÓN DE PARAMETROS: Definir los parámetros con tipos explícitos
                                var loteIdParam = new SqlParameter("@p0", SqlDbType.Int) { Value = loteId };

                                // CRÍTICO: Forzar el tipo a Decimal para coincidir con la DB y evitar la inferencia a bigint.
                                var zeroAmountParam = new SqlParameter("@p1", SqlDbType.Decimal)
                                {
                                    Value = zeroAmount,
                                    Precision = 18, // Asumiendo precisión estándar
                                    Scale = 2       // Asumiendo dos decimales
                                };

                                // Contar ANTES de la operación 
                                registrosReseteados = await _db.Ventas.AsNoTracking()
                                   .Where(v => v.CargaLoteId == loteId && v.MontoTotal > zeroAmount)
                                   .LongCountAsync();

                                registrosEliminadosVentaCero = await _db.Ventas.AsNoTracking()
                                   .Where(v => v.CargaLoteId == loteId && v.MontoTotal == zeroAmount)
                                   .CountAsync();


                                // Paso A: UPDATE para desvincular (CargaLoteId = NULL) las Ventas con Monto (>0)
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
                                    new[] { loteIdParam, zeroAmountParam }); // Pasamos los parámetros explícitos

                                // Paso B: DELETE para eliminar las Ventas Cero (=0)
                                // Reiniciamos los parámetros para evitar problemas de estado
                                loteIdParam.Value = loteId;
                                zeroAmountParam.Value = zeroAmount;

                                await _db.Database.ExecuteSqlRawAsync(
                                    @"DELETE FROM dbo.Ventas
                                WHERE CargaLoteId = @p0 AND MontoTotal = @p1",
                                    new[] { loteIdParam, zeroAmountParam }); // Pasamos los parámetros explícitos
                            }

                            // 2. Eliminar el registro padre (CargasLotes)
                            _db.CargasLotes.Remove(lote);
                            await _db.SaveChangesAsync();

                            // 3. Confirmar la transacción
                            await transaction.CommitAsync();
                        }
                        catch (Exception)
                        {
                            await transaction.RollbackAsync();
                            throw;
                        }
                    }
                });

                // 4. Mensajes de éxito
                if (tipoArchivo == "Retenciones")
                {
                    MensajeCarga = $"✅ ÉXITO: Lote de **{tipoArchivo}** eliminado correctamente. Se eliminaron **{registrosEliminadosVentaCero}** registros (Ventas Cero) y se **resetearon {registrosReseteados}** registros de venta existentes.";
                }
                else
                {
                    MensajeCarga = $"✅ ÉXITO: Lote de **{tipoArchivo}** eliminado correctamente. Se eliminaron **{lote.TotalRegistros}** registros de compra asociados.";
                }
            }
            catch (Exception ex)
            {
                // Si vuelve a fallar aquí, el problema es que la columna CargaLoteId en la DB no permite NULLs.
                MensajeCarga = $"❌ **ERROR CRÍTICO AL ELIMINAR LOTE:** No se pudo completar la operación. Detalle: {ex.InnerException?.Message ?? ex.Message}";
            }

            await OnGetAsync();
            return Page();
        }
    };
}