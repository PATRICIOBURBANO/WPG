using AtsManager.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Globalization;

namespace AtsManager.Services
{
    public class XmlBatchImporter
    {
        private readonly AtsDbContext _context;
        private readonly ATSXmlGenerator _atsGenerator;

        // Constructor, métodos auxiliares (GetFirstDescendant, GetChildValue, ObtenerFechaEmision) y ImportarDesdeCarpeta
        // ... (Se mantienen sin cambios para brevedad)

        // El resto de la clase XmlBatchImporter (métodos auxiliares) debe estar aquí.

        public XmlBatchImporter(AtsDbContext context, ATSXmlGenerator atsGenerator)
        {
            _context = context;
            _atsGenerator = atsGenerator;
        }

        private XElement? GetFirstDescendant(XDocument xmlDoc, string localName)
        {
            return xmlDoc.Descendants()
                         .FirstOrDefault(e => e.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase));
        }

        private string? GetChildValue(XElement parent, string localName)
        {
            return parent.Descendants()
                         .FirstOrDefault(e => e.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase))?.Value;
        }

        private DateTime? ObtenerFechaEmision(XDocument xmlDoc)
        {
            const string dateFormat = "dd/MM/yyyy";
            var infoFactura = GetFirstDescendant(xmlDoc, "infoFactura");
            if (infoFactura != null)
            {
                string fechaStr = GetChildValue(infoFactura, "fechaEmision") ?? "";
                if (DateTime.TryParseExact(fechaStr, dateFormat, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime fechaFactura))
                {
                    return fechaFactura;
                }
            }
            var infoRetencion = GetFirstDescendant(xmlDoc, "infoCompRetencion");
            if (infoRetencion != null)
            {
                string fechaStr = GetChildValue(infoRetencion, "fechaEmision") ?? "";
                if (DateTime.TryParseExact(fechaStr, dateFormat, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime fechaRetencion))
                {
                    return fechaRetencion;
                }
            }
            return null;
        }

        // ---------------------------------------------------------------------
        // --- NUEVO MÉTODO AUXILIAR PARA LA SEGREGACIÓN DE BASES ---
        // ---------------------------------------------------------------------

        /// <summary>
        /// Calcula la Base Gravada (15%) y la Base No Gravada/Tarifa Cero por diferencia.
        /// Asigna los resultados a BaseImpGrav y BaseNoGraIva del objeto Compra.
        /// </summary>
        private void SegregarBasesImponibles(Compra compra)
        {
            // Usamos el 15% como factor de IVA
            const decimal TARIFA_IVA = 0.15m;

            // CORRECCIÓN 1: Obtenemos el valor de BaseImponible directamente, ya que es un decimal
            // y no necesita ser parseado. Usamos el operador ?? 0.00M por si BaseImponible fuera nullable.
            decimal valorSinImpuestosTotal = compra.BaseImponible; // Asumiendo que BaseImponible es decimal NO NULL.
            decimal ivaCalculado = compra.MontoIva ?? 0.00m; // MontoIva es decimal?, usamos 0 si es null

            // CORRECCIÓN 2: Aseguramos que los campos de destino sean 0.00M (o null, si lo prefieres, pero 0.00M es más seguro para cálculos)
            compra.BaseImpGrav = 0.00M;
            compra.BaseNoGraIva = 0.00M;
            compra.BaseImpExe = 0.00M; // También inicializamos BaseImpExe por si aplica

            if (ivaCalculado > 0)
            {
                // 1. Calcular la Base Gravada Real (el monto que realmente generó el IVA)
                // Usamos MidpointRounding.AwayFromZero para una gestión de redondeo estándar (al centavo).
                decimal baseGravadaReal = Math.Round(ivaCalculado / TARIFA_IVA, 2, MidpointRounding.AwayFromZero);

                // 2. Determinar la Base No Gravada por diferencia
                // Esto maneja facturas combinadas (Gravada 15% + Tarifa Cero/No Objeto).
                decimal baseNoGravada = valorSinImpuestosTotal - baseGravadaReal;

                // Asignación de la Base Gravada
                compra.BaseImpGrav = baseGravadaReal;

                // Asignar el sobrante a BaseNoGraIva si es positivo.
                if (baseNoGravada > 0)
                {
                    // El sobrante es la porción Tarifa Cero o No Objeto (se asigna a BaseNoGraIva)
                    compra.BaseNoGraIva = Math.Round(baseNoGravada, 2, MidpointRounding.AwayFromZero);
                }
            }
            else
            {
                // Si el IVA es 0, toda la Base Imponible se considera No Gravada (Tarifa Cero o No Objeto).
                compra.BaseNoGraIva = valorSinImpuestosTotal;
            }
        }

        // ---------------------------------------------------------------------
        // --- MÉTODO ProcesarFactura MODIFICADO ---
        // ---------------------------------------------------------------------

        private Compra? ProcesarFactura(XDocument xmlDoc, List<string> resultados)
        {
            var infoTributaria = GetFirstDescendant(xmlDoc, "infoTributaria");
            var infoComprobante = GetFirstDescendant(xmlDoc, "infoFactura");

            // ... (Lógica de validación y extracción de RUC, Razón Social, NumComprobante)
            if (infoTributaria == null || infoComprobante == null)
            {
                resultados.Add($"ADVERTENCIA PARSEO: No se encontraron los nodos infoTributaria/infoFactura. Archivo omitido.");
                return null;
            }

            string rucProveedor = GetChildValue(infoTributaria, "ruc") ?? "N/A";
            string razonSocialProveedor = GetChildValue(infoTributaria, "razonSocial") ?? "N/A";
            string estab = GetChildValue(infoTributaria, "estab") ?? "000";
            string ptoEmi = GetChildValue(infoTributaria, "ptoEmi") ?? "000";
            string secuencial = GetChildValue(infoTributaria, "secuencial") ?? "000000000";
            string numComprobante = $"{estab}-{ptoEmi}-{secuencial}";

            // 💡 DECIMAL PARSING SEGURO
            decimal.TryParse(GetChildValue(infoComprobante, "totalSinImpuestos") ?? "0", System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal baseImp);
            decimal.TryParse(GetChildValue(infoComprobante, "importeTotal") ?? "0", System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal montoTotal);

            // 💡 IVA SUM SEGURO
            decimal montoIva = xmlDoc.Descendants()
                .Where(e => e.Name.LocalName.Equals("totalImpuesto", StringComparison.OrdinalIgnoreCase) && GetChildValue(e, "codigo") == "2")
                .Sum(ti => decimal.Parse(GetChildValue(ti, "valor") ?? "0", System.Globalization.CultureInfo.InvariantCulture));

            DateTime fechaEmision = ObtenerFechaEmision(xmlDoc) ?? DateTime.Now.Date;
            string tipoIdProv;
            if (rucProveedor.Length == 13 && rucProveedor.EndsWith("001"))
            {
                tipoIdProv = "01"; // RUC
            }
            else if (rucProveedor.Length == 10)
            {
                tipoIdProv = "02"; // Cédula
            }
            else
            {
                tipoIdProv = "03"; // Consumidor Final (o '06' para Otro/Pasaporte, dependiendo de tu requerimiento)
            }
            var compra = new Compra
            {
                // ... (Asignaciones de campos de texto y fechas)
                IdProveedor = rucProveedor,
                TipoComprobante = GetChildValue(infoTributaria, "codDoc") ?? "01",
                NumComprobante = numComprobante,
                Autorizacion = GetChildValue(infoTributaria, "claveAcceso") ?? "",
                FechaEmision = fechaEmision,
                FechaRegistro = fechaEmision,
                UsuarioCreacion = "ImportadorXML",
                RazonSocialProveedor = razonSocialProveedor,
                Anio = (short)fechaEmision.Year,
                Mes = (short)fechaEmision.Month,
                CodSustento = "01", // 💡 Por defecto para facturas locales (Factura Electrónica)
                TipoIdProveedor = tipoIdProv,

                // ERROR 3 & 4: puntoEmision y secuencial (estaban mal formateados)
                // Guardamos los componentes por separado para que el generador DIMM no tenga que 'dividir' el NumComprobante.
                
                Estab = estab,
                PtoEmi = ptoEmi,
                Secuencial = secuencial,

                // Estos campos se llenan con los totales del XML
                BaseImponible = baseImp, // VALOR_SIN_IMPUESTOS TOTAL
                MontoTotal = montoTotal,
                MontoIva = montoIva,
                BaseImpGrav = baseImp,
                BaseNoGraIva = montoTotal-baseImp-montoIva,


                // BaseImpGrav y BaseNoGraIva quedan en NULL o 0.00m aquí, se llenan a continuación
            };

            // 🔑 APLICAR LA LÓGICA DE SEGREGACIÓN para llenar BaseImpGrav y BaseNoGraIva
           
            resultados.Add($"DIAGNÓSTICO PERSISTENCIA - {compra.NumComprobante} | B. Gravada: {compra.BaseImpGrav} | B. No Gravada: {compra.BaseNoGraIva} | IVA: {compra.MontoIva}");
            return compra;
        }

        // ... (El resto de la clase, incluyendo ImportarDesdeCarpeta, ProcesarComprobanteRetencion, etc.)

        // El código del método ImportarDesdeCarpeta va aquí (sin cambios)
        public List<string> ImportarDesdeCarpeta(string rutaCarpeta)
        {
            var resultados = new List<string>();
            if (!Directory.Exists(rutaCarpeta))
            {
                resultados.Add($"ERROR: La carpeta no existe: {rutaCarpeta}");
                return resultados;
            }

            string[] archivosXml = Directory.GetFiles(rutaCarpeta, "*.xml", SearchOption.TopDirectoryOnly);
            resultados.Add($"Archivos XML encontrados: {archivosXml.Length}");

            var ventasNuevas = new List<Venta>();
            var ventasActualizadas = new List<Venta>();
            var comprasNuevas = new List<Compra>();

            // --- 1. PREPARACIÓN DEL LOTE (BUSCAR O CREAR) ---
            // ... (Lógica de obtención de fechaBaseLote, anioActual, mesActual) ...

            DateTime fechaBaseLote = DateTime.Now;
            if (archivosXml.Length > 0)
            {
                try
                {
                    XDocument rawXmlDoc = XDocument.Load(archivosXml[0]);
                    XDocument xmlDocBase = rawXmlDoc;
                    XElement root = rawXmlDoc.Root;

                    if (root != null && root.Name.LocalName.Equals("autorizacion", StringComparison.OrdinalIgnoreCase))
                    {
                        string comprobanteXmlString = root.Descendants()
                            .FirstOrDefault(e => e.Name.LocalName.Equals("comprobante", StringComparison.OrdinalIgnoreCase))?.Value;
                        if (!string.IsNullOrEmpty(comprobanteXmlString))
                        {
                            xmlDocBase = XDocument.Parse(comprobanteXmlString);
                        }
                    }

                    DateTime? fechaLoteProcesada = ObtenerFechaEmision(xmlDocBase);
                    if (fechaLoteProcesada.HasValue)
                    {
                        fechaBaseLote = fechaLoteProcesada.Value;
                    }
                    else
                    {
                        resultados.Add($"❌ ERROR PARSEO FECHA: No se pudo obtener una fecha de emisión válida del primer XML. Usando fecha actual: {fechaBaseLote:MM/yyyy}.");
                    }
                }
                catch (Exception ex)
                {
                    resultados.Add($"❌ ERROR CRÍTICO al cargar el primer XML para obtener la fecha del lote: {ex.Message}");
                }
            }

            short anioActual = (short)fechaBaseLote.Year;
            short mesActual = (short)fechaBaseLote.Month;
            const string tipoArchivo = "XML";

            var nuevoLote = _context.CargasLotes
                .FirstOrDefault(l =>
                    l.Anio == anioActual &&
                    l.Mes == mesActual &&
                    l.TipoArchivo == tipoArchivo);

            if (nuevoLote == null)
            {
                // CASO: Lote Nuevo
                nuevoLote = new CargaLote
                {
                    Anio = anioActual,
                    Mes = mesActual,
                    TipoArchivo = tipoArchivo,
                    NombreArchivo = $"Lote XML {anioActual}-{mesActual:00} - {DateTime.Now:yyyyMMddHHmmss}",
                    FechaCarga = DateTime.Now
                };
            }
            else
            {
                // CASO: Lote Existente (Limpieza y Reutilización)
                int loteIdExistente = nuevoLote.Id;

                // 🎯 FIX CRÍTICO 1: Desacoplar el lote padre (necesario para la actualización final).
                _context.Entry(nuevoLote).State = EntityState.Detached;

                try
                {
                    // 1. LIMPIEZA CON RAW SQL: Eliminamos directamente los hijos con SQL.
                    // ESTO ES LO QUE RESUELVE EL PROBLEMA DE FK.
                    string sqlVentas = $"DELETE FROM Ventas WHERE CargaLoteId = {loteIdExistente}";
                    string sqlCompras = $"DELETE FROM Compras WHERE CargaLoteId = {loteIdExistente}";

                    // Ejecutamos las eliminaciones. (Reemplaza el _context.RemoveRange y _context.SaveChanges)
                    int deletedVentas = _context.Database.ExecuteSqlRaw(sqlVentas);
                    int deletedCompras = _context.Database.ExecuteSqlRaw(sqlCompras);

                    int totalRegistrosEliminados = deletedVentas + deletedCompras;

                    if (totalRegistrosEliminados > 0)
                    {
                        resultados.Add($"LIMPIEZA: Se eliminaron los {totalRegistrosEliminados} registros anteriores del lote ID: {loteIdExistente} para re-importar (Usando SQL Directo).");
                    }

                    // 2. Resetear el contador en el objeto en memoria
                    nuevoLote.TotalRegistros = 0;

                    // 3. Volver a adjuntar el lote como 'Unchanged' para que pueda ser actualizado al final.
                    _context.CargasLotes.Attach(nuevoLote);
                }
                catch (Exception ex)
                {
                    resultados.Add($"❌ ERROR CRÍTICO DE BD (Limpieza de Lote - RAW SQL): Falló al limpiar el lote. Detalle: {ex.InnerException?.Message ?? ex.Message}");
                    return resultados;
                }

                resultados.Add($"ADVERTENCIA LOTE: Se utilizará el lote existente ID: {nuevoLote.Id} ({anioActual}-{mesActual})");
            }

            short anioLote = anioActual;
            short mesLote = mesActual;
            // --- FIN PREPARACIÓN LOTE ---


            foreach (var filePath in archivosXml)
            {
                string fileName = Path.GetFileName(filePath);
                XDocument xmlDoc = null;

                try
                {
                    XDocument rawXmlDoc = XDocument.Load(filePath);
                    XElement root = rawXmlDoc.Root;

                    // LÓGICA DE EXTRACCIÓN DEL COMPROBANTE REAL (CDATA)
                    if (root != null && root.Name.LocalName.Equals("autorizacion", StringComparison.OrdinalIgnoreCase))
                    {
                        string comprobanteXmlString = root.Descendants()
                            .FirstOrDefault(e => e.Name.LocalName.Equals("comprobante", StringComparison.OrdinalIgnoreCase))?.Value;

                        if (!string.IsNullOrEmpty(comprobanteXmlString))
                        {
                            xmlDoc = XDocument.Parse(comprobanteXmlString);
                        }
                        else
                        {
                            xmlDoc = rawXmlDoc;
                        }
                    }
                    else
                    {
                        xmlDoc = rawXmlDoc;
                    }

                    if (xmlDoc == null || xmlDoc.Root == null)
                    {
                        resultados.Add($"❌ ERROR ESTRUCTURA: No se pudo determinar el documento XML interno para ({fileName}).");
                        continue;
                    }

                    // VALIDACIÓN CLAVE: COMPROBAR LA FECHA DEL DOCUMENTO PERTENEZCA AL LOTE
                    DateTime? fechaComprobanteNullable = ObtenerFechaEmision(xmlDoc);

                    if (!fechaComprobanteNullable.HasValue)
                    {
                        resultados.Add($"ADVERTENCIA FECHA: No se pudo determinar la fecha de emisión. Comprobante ignorado ({fileName}).");
                        continue;
                    }

                    DateTime fechaComprobante = fechaComprobanteNullable.Value;

                    if (fechaComprobante.Year != anioLote || fechaComprobante.Month != mesLote)
                    {
                        resultados.Add($"ADVERTENCIA PERIODO: Comprobante de {fechaComprobante:MM/yyyy} ignorado. No pertenece al periodo del lote ({mesLote:00}/{anioLote}). ({fileName})");
                        continue; // Saltar al siguiente archivo
                    }

                    // --- PROCESAMIENTO (solo si la fecha y el periodo son correctos) ---

                    if (xmlDoc.Root.Name.LocalName.Equals("factura", StringComparison.OrdinalIgnoreCase))
                    {
                        // Factura ahora es una Compra
                        var compra = ProcesarFactura(xmlDoc, resultados);
                        if (compra != null) comprasNuevas.Add(compra);
                        resultados.Add($"OK: Factura de Compra procesada ({fileName})");
                    }
                    else if (xmlDoc.Root.Name.LocalName.Equals("comprobanteRetencion", StringComparison.OrdinalIgnoreCase))
                    {
                        // Retención es de Venta
                        ProcesarComprobanteRetencion(xmlDoc, ventasNuevas, ventasActualizadas, comprasNuevas, resultados);
                        resultados.Add($"OK: Comprobante de Retención de Venta procesado ({fileName})");
                    }
                    else
                    {
                        resultados.Add($"ADVERTENCIA: Archivo ignorado. Tipo de comprobante no reconocido ({xmlDoc.Root.Name.LocalName}) ({fileName})");
                    }
                }
                catch (Exception ex)
                {
                    resultados.Add($"❌ ERROR CRÍTICO al procesar {fileName}: {ex.InnerException?.Message ?? ex.Message}");
                }
            }

            // --- 2. IMPLEMENTACIÓN DEL GUARDADO FINAL ---

            int totalRegistros = ventasNuevas.Count + ventasActualizadas.Count + comprasNuevas.Count;

            if (totalRegistros > 0)
            {
                if (nuevoLote.Id == 0)
                {
                    // Lote Nuevo
                    nuevoLote.TotalRegistros = totalRegistros;
                    _context.CargasLotes.Add(nuevoLote);

                    try
                    {
                        _context.SaveChanges();
                    }
                    catch (Exception ex)
                    {
                        resultados.Add($"❌ ERROR CRÍTICO DE BD (Lote Padre): Falló al guardar el lote principal. Detalle: {ex.InnerException?.Message ?? ex.Message}");
                        return resultados;
                    }
                }
                else
                {
                    // Lote Existente: Actualizar TotalRegistros
                    nuevoLote.TotalRegistros += totalRegistros;
                    _context.CargasLotes.Update(nuevoLote); // Marca el lote Attached como 'Modified'
                }

                int loteIdReal = nuevoLote.Id;

                // PASO 2: Asignar el ID real a los hijos, rastrear y guardar

                // 2a. Compras Nuevas
                foreach (var compra in comprasNuevas)
                {
                    compra.CargaLoteId = loteIdReal;
                }
                _context.Compras.AddRange(comprasNuevas);
                // 2b. Ventas Nuevas
                foreach (var venta in ventasNuevas) { venta.CargaLoteId = loteIdReal; }
                _context.Ventas.AddRange(ventasNuevas);

                // 2c. Ventas Actualizadas (Retenciones)
                foreach (var venta in ventasActualizadas) { venta.CargaLoteId = loteIdReal; _context.Ventas.Update(venta); }

                try
                {
                    // GUARDADO FINAL: Guarda Compras, Ventas, y el UPDATE del Lote Padre.
                    _context.SaveChanges();
                    resultados.Add($"✅ ÉXITO: {totalRegistros} registros guardados en lote {loteIdReal}.");
                }
                catch (Exception ex)
                {
                    resultados.Add($"❌ ERROR CRÍTICO DE BD (Registros Hijos): No se pudieron guardar los detalles. Posible Duplicado o FK no válida. Detalle: {ex.InnerException?.Message ?? ex.Message}");
                    return resultados;
                }
            }
            else
            {
                resultados.Add("ADVERTENCIA: No se encontraron registros válidos para guardar. (El parseo de XML falló o no coincidió con el periodo del lote).");
            }

            return resultados;
        }
        private void ProcesarComprobanteRetencion(XDocument xmlDoc, List<Venta> ventasNuevas, List<Venta> ventasActualizadas, List<Compra> comprasNuevas, List<string> resultados)
        {
            var infoTributaria = GetFirstDescendant(xmlDoc, "infoTributaria");
            var infoCompRetencion = GetFirstDescendant(xmlDoc, "infoCompRetencion");

            var impuestos = xmlDoc.Descendants().Where(e => e.Name.LocalName.Equals("impuesto", StringComparison.OrdinalIgnoreCase));

            if (infoTributaria == null || infoCompRetencion == null)
            {
                resultados.Add($"ADVERTENCIA PARSEO: No se encontraron los nodos infoTributaria/infoCompRetencion. Archivo omitido.");
                return;
            }

            string estab = GetChildValue(infoTributaria, "estab") ?? "000";
            string ptoEmi = GetChildValue(infoTributaria, "ptoEmi") ?? "000";
            string secuencial = GetChildValue(infoTributaria, "secuencial") ?? "000000000";
            string numRetencionUnformatted = estab + ptoEmi + secuencial;

            string periodoFiscal = GetChildValue(infoCompRetencion, "periodoFiscal") ?? ""; // Ej: "09/2025"
            short anioRet = 0;
            short mesRet = 0;

            if (!string.IsNullOrWhiteSpace(periodoFiscal) && periodoFiscal.Contains('/') && periodoFiscal.Split('/').Length == 2)
            {
                if (short.TryParse(periodoFiscal.Split('/')[0], out short m) && short.TryParse(periodoFiscal.Split('/')[1], out short a))
                {
                    mesRet = m;
                    anioRet = a;
                }
            }

            // 🎯 FIX CRÍTICO: Declarar y asignar fechaRetencion AQUI 🎯
            DateTime? fechaRetencion = ObtenerFechaEmision(xmlDoc);

            string rucCliente = GetChildValue(infoCompRetencion, "identificacionSujetoRetenido") ?? "";
            string razonSocialCliente = GetChildValue(infoCompRetencion, "razonSocialSujetoRetenido") ?? "N/A";
            string numComprobanteSustento = GetChildValue(infoCompRetencion, "numDocSustento") ?? ""; // 💡 Safety

            // 1. Calcular los totales retenidos
            decimal totalRetIVA = impuestos
                .Where(i => GetChildValue(i, "codigo") == "2" && GetChildValue(i, "codigoRetencion") != null)
                .Sum(i => decimal.Parse(GetChildValue(i, "valorRetenido") ?? "0", System.Globalization.CultureInfo.InvariantCulture));

            decimal totalRetRenta = impuestos
                .Where(i => GetChildValue(i, "codigo") == "1" && GetChildValue(i, "codigoRetencion") != null)
                .Sum(i => decimal.Parse(GetChildValue(i, "valorRetenido") ?? "0", System.Globalization.CultureInfo.InvariantCulture));

            // 2. Intentar encontrar la Venta Sustentada
            var ventaExistente = _context.Ventas
                .AsNoTracking()
                .FirstOrDefault(v => v.NumComprobante == numComprobanteSustento && v.IdCliente == rucCliente);

            // 3. Procesar: Actualizar existente o Crear Venta Cero
            if (ventaExistente != null)
            {
                ventaExistente.valRetIVA = totalRetIVA;
                ventaExistente.valRetRenta = totalRetRenta;
                ventaExistente.NumRetencion = numRetencionUnformatted;
                ventaExistente.Estab = estab;
                ventaExistente.PtoEmi = ptoEmi;
                ventaExistente.Secuencial = secuencial;
                ventaExistente.Anio = anioRet; // Usamos el periodo fiscal
                ventaExistente.Mes = mesRet;   // Usamos el periodo fiscal

                ventaExistente.AutorizacionRetencion = GetChildValue(infoTributaria, "claveAcceso") ?? "";
                ventaExistente.FechaRetencion = fechaRetencion; // ✅ Ahora esta variable existe
                ventaExistente.RazonSocialCliente = razonSocialCliente;

                ventasActualizadas.Add(ventaExistente);
                resultados.Add($"RET VENTA: Actualizada para {numComprobanteSustento} y agregada a la lista de guardado.");
            }
            else
            {
                var nuevaVentaCero = CrearVentaCeroRetencion(infoTributaria, infoCompRetencion, impuestos, totalRetIVA, totalRetRenta, razonSocialCliente, estab, ptoEmi, secuencial, mesRet, anioRet, fechaRetencion); // ✅ Ahora se pasa correctamente
                ventasNuevas.Add(nuevaVentaCero);
                resultados.Add($"ADVERTENCIA RET VENTA: La factura de venta {numComprobanteSustento} no se encontró. Se CREÓ VENTA CERO para registrar la retención.");
            }
        }

        private Venta CrearVentaCeroRetencion(XElement infoTributaria, XElement infoCompRetencion, IEnumerable<XElement> impuestos,
    decimal totalRetIVA, decimal totalRetRenta, string razonSocialCliente,
    string estab, string ptoEmi, string secuencial, short mesRet, short anioRet, DateTime? fechaRetencion)
        {
            // 1. Número de Retención (Serie completa)
            string numRetencionUnformatted = estab + ptoEmi + secuencial;

            // 2. Fecha de Emisión del Documento Sustento (Fecha de la Factura de Venta)
            DateTime? fechaEmisionDoc = null;
            string fechaDocStr = GetChildValue(infoCompRetencion, "fechaEmisionDocSustento") ?? "";
            const string dateFormat = "dd/MM/yyyy";
            if (DateTime.TryParseExact(fechaDocStr, dateFormat, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime parsedDate))
            {
                fechaEmisionDoc = parsedDate;
            }

            // 💡 IMPORTANTE: La variable fechaRetencion (parámetro) se usa directamente.

            var venta = new Venta
            {
                // --- MONTOS Y RETENCIONES ---
                valRetRenta = totalRetRenta,
                valRetIVA = totalRetIVA,
                MontoTotal = 0.00m,
                BaseImponible = 0.00m,
                MontoIva = 0.00m,

                // --- INFORMACIÓN DEL DOCUMENTO DE RETENCIÓN (Cliente) ---
                NumRetencion = numRetencionUnformatted,
                AutorizacionRetencion = GetChildValue(infoTributaria, "claveAcceso") ?? "",
                FechaRetencion = fechaRetencion, // ✅ Usa el parámetro de entrada (FIX)

                // --- INFORMACIÓN DEL PERIODO FISCAL ---
                Anio = anioRet,
                Mes = mesRet,

                // --- COMPONENTES DE LA SERIE DE RETENCIÓN (FIX para el modelo Venta) ---
                Estab = estab,
                PtoEmi = ptoEmi,
                Secuencial = secuencial,

                // --- INFORMACIÓN DEL DOCUMENTO SUSTENTO (Factura de Venta) ---
                TipoComprobante = GetChildValue(infoCompRetencion, "codDocSustento") ?? "01",
                NumComprobante = GetChildValue(infoCompRetencion, "numDocSustento") ?? "",
                FechaEmision = fechaEmisionDoc ?? DateTime.MinValue,
                IdCliente = GetChildValue(infoCompRetencion, "identificacionSujetoRetenido") ?? "",
                RazonSocialCliente = razonSocialCliente,

                // --- METADATOS ---
                UsuarioCreacion = "CargaRetencionXMLCero",
                FechaCreacion = DateTime.Now,
            };

            return venta;
        }

        private Compra CrearEntidadCompraRetencion(XElement infoTributaria, XElement infoCompRetencion, IEnumerable<XElement> impuestos, string rucProveedor, string claveAcceso, DateTime fechaEmisionDoc, string numRetencion)
        {
            // ... (código original, ajustado para seguridad de cadenas)
            string estab = GetChildValue(infoTributaria, "estab") ?? "000";
            string ptoEmi = GetChildValue(infoTributaria, "ptoEmi") ?? "000";
            string secuencial = GetChildValue(infoTributaria, "secuencial") ?? "000000000";

            string numComprobanteRetencion = $"{estab}-{ptoEmi}-{secuencial}";
            string tipoComprobante = GetChildValue(infoCompRetencion, "codDocSustento") ?? "";

            return new Compra
            {
                IdProveedor = rucProveedor,
                NumComprobante = numComprobanteRetencion,
                TipoComprobante = tipoComprobante,
                Autorizacion = claveAcceso,
                FechaEmision = fechaEmisionDoc,
                FechaRegistro = DateTime.Now,

                ValRetAir = impuestos
                    .Where(i => GetChildValue(i, "codigo") == "1")
                    .Sum(i => decimal.Parse(GetChildValue(i, "valorRetenido") ?? "0", System.Globalization.CultureInfo.InvariantCulture)),

                ValorRetServicios = impuestos
                    .Where(i => GetChildValue(i, "codigo") == "2")
                    .Sum(i => decimal.Parse(GetChildValue(i, "valorRetenido") ?? "0", System.Globalization.CultureInfo.InvariantCulture)),

                RazonSocialProveedor = GetChildValue(infoCompRetencion, "razonSocialSujetoRetenido") ?? "N/A",
                BaseImponible = 0.00M,
                MontoTotal = 0.00M,
                UsuarioCreacion = "ImportadorXML",
                FechaCreacion = DateTime.Now
            };
        }
    }
}