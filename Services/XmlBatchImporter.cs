using AtsManager.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Globalization;
using AtsManager.Services;


namespace AtsManager.Services
{
    public class XmlBatchImporter
    {
        private const string CONTEXTO_RECIBIDOS = "RECIBIDOS";
        private const string CONTEXTO_EMITIDOS = "EMITIDOS";

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
            
            // Buscar en infoFactura (facturas)
            var infoFactura = GetFirstDescendant(xmlDoc, "infoFactura");
            if (infoFactura != null)
            {
                string fechaStr = GetChildValue(infoFactura, "fechaEmision") ?? "";
                if (DateTime.TryParseExact(fechaStr, dateFormat, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime fechaFactura))
                {
                    return fechaFactura;
                }
            }
            
            // Buscar en infoNotaCredito (notas de crédito)
            var infoNotaCredito = GetFirstDescendant(xmlDoc, "infoNotaCredito");
            if (infoNotaCredito != null)
            {
                string fechaStr = GetChildValue(infoNotaCredito, "fechaEmision") ?? "";
                if (DateTime.TryParseExact(fechaStr, dateFormat, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime fechaNC))
                {
                    return fechaNC;
                }
            }
            
            // Buscar en infoNotaDebito (notas de débito)
            var infoNotaDebito = GetFirstDescendant(xmlDoc, "infoNotaDebito");
            if (infoNotaDebito != null)
            {
                string fechaStr = GetChildValue(infoNotaDebito, "fechaEmision") ?? "";
                if (DateTime.TryParseExact(fechaStr, dateFormat, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime fechaND))
                {
                    return fechaND;
                }
            }
            
            // Buscar en infoCompRetencion (retenciones)
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
            decimal valorSinImpuestosTotal = compra.BaseImponible.GetValueOrDefault(); // Asumiendo que BaseImponible es decimal NO NULL.
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

        private Compra? ProcesarFactura(XDocument xmlDoc, List<string> resultados, string rucEmisor = "")
        {
            var infoTributaria = GetFirstDescendant(xmlDoc, "infoTributaria");
            var infoComprobante = GetFirstDescendant(xmlDoc, "infoFactura");

            // Extraer RUC de la empresa(emisor) del XML - ES EL RUC DE NUESTRA EMPRESA, NO DEL PROVEEDOR
            if (infoTributaria == null || infoComprobante == null)
            {
                resultados.Add($"ADVERTENCIA PARSEO: No se encontraron los nodos infoTributaria/infoFactura. Archivo omitido.");
                return null;
            }

            string rucProveedor = GetChildValue(infoTributaria, "ruc") ?? "N/A";
            string razonSocialProveedor = GetChildValue(infoTributaria, "razonSocial") ?? "N/A";
            string estab = NormalizarCodigo(GetChildValue(infoTributaria, "estab") ?? "", 3);
            string ptoEmi = NormalizarCodigo(GetChildValue(infoTributaria, "ptoEmi") ?? "", 3);
            string secuencial = NormalizarSecuencial(GetChildValue(infoTributaria, "secuencial") ?? "");
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
                RucEmpresa = rucEmisor,
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

                // Forma de pago
                FormaPago = "01",

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

        // ---------------------------------------------------------------------
        // --- PROCESAR NOTA CRÉDITO (COMPRA) ---
        // ---------------------------------------------------------------------
        private Compra? ProcesarNotaCredito(XDocument xmlDoc, List<string> resultados, string rucEmisor = "")
        {
            var infoTributaria = GetFirstDescendant(xmlDoc, "infoTributaria");
            var infoNotaCredito = GetFirstDescendant(xmlDoc, "infoNotaCredito");

            if (infoTributaria == null || infoNotaCredito == null)
            {
                resultados.Add("ADVERTENCIA: No se encontraron nodos para Nota Crédito.");
                return null;
            }

            string rucProveedor = GetChildValue(infoTributaria, "ruc") ?? "";
            string razonSocialProveedor = GetChildValue(infoTributaria, "razonSocial") ?? "";
            string estab = NormalizarCodigo(GetChildValue(infoTributaria, "estab") ?? "", 3);
            string ptoEmi = NormalizarCodigo(GetChildValue(infoTributaria, "ptoEmi") ?? "", 3);
            string secuencial = NormalizarSecuencial(GetChildValue(infoTributaria, "secuencial") ?? "");
            string numComprobante = $"{estab}-{ptoEmi}-{secuencial}";

            // Datos del documento modificado
            string tipoModificado = "";
            string estabModificado = "";
            string ptoEmiModificado = "";
            string secuencialModificado = "";
            string autorizacionModificada = "";
            
            // Leer datos del documento modificado desde los campos correctos
            string codDocModificado = GetChildValue(infoNotaCredito, "codDocModificado") ?? "";
            string numDocModificado = GetChildValue(infoNotaCredito, "numDocModificado") ?? "";
            
            if (!string.IsNullOrEmpty(numDocModificado))
            {
                string limpio = numDocModificado.Replace("-", "").Replace(" ", "");
                var digitos = new string(limpio.Where(char.IsDigit).ToArray());
                if (digitos.Length >= 15)
                {
                    tipoModificado = codDocModificado.Length >= 2 ? codDocModificado.Substring(0, 2) : "01";
                    estabModificado = digitos.Length >= 3 ? digitos.Substring(0, 3) : "001";
                    ptoEmiModificado = digitos.Length >= 6 ? digitos.Substring(3, 3) : "001";
                    secuencialModificado = digitos.Length >= 15 ? digitos.Substring(6, 9) : "000000001";
                }
                else if (digitos.Length >= 9)
                {
                    tipoModificado = codDocModificado.Length >= 2 ? codDocModificado.Substring(0, 2) : "01";
                    estabModificado = "001";
                    ptoEmiModificado = "001";
                    secuencialModificado = digitos.Substring(0, 9).PadLeft(9, '0');
                }
            }

            // Obtener forma de pago
            string formaPago = "01";
            var infoPago = GetFirstDescendant(xmlDoc, "infoPago");
            if (infoPago != null)
            {
                formaPago = GetChildValue(infoPago, "formaPago") ?? "01";
            }
            
            decimal.TryParse(GetChildValue(infoNotaCredito, "totalSinImpuestos") ?? "0", System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal baseImp);
            decimal.TryParse(GetChildValue(infoNotaCredito, "importeTotal") ?? "0", System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal montoTotal);

            decimal montoIva = xmlDoc.Descendants()
                .Where(e => e.Name.LocalName.Equals("totalImpuesto", StringComparison.OrdinalIgnoreCase) && GetChildValue(e, "codigo") == "2")
                .Sum(ti => decimal.Parse(GetChildValue(ti, "valor") ?? "0", System.Globalization.CultureInfo.InvariantCulture));

            DateTime? fechaEmision = ObtenerFechaEmision(xmlDoc);

            var compra = new Compra
            {
                RucEmpresa = rucEmisor,
                CodigoCompra = $"NC{DateTime.Now.Ticks % 100000}",
                TipoComprobante = "04", // Nota Crédito
                NumComprobante = numComprobante,
                IdProveedor = rucProveedor,
                RazonSocialProveedor = razonSocialProveedor,
                TipoIdProveedor = rucProveedor.Length == 13 ? "01" : "02",
                BaseImponible = baseImp,
                MontoIva = montoIva,
                MontoTotal = montoTotal,
                CodSustento = montoIva > 0 ? "01" : "02",
                FechaEmision = fechaEmision,
                FechaRegistro = fechaEmision,
                Anio = fechaEmision.HasValue ? (short)fechaEmision.Value.Year : (short)DateTime.Now.Year,
                Mes = fechaEmision.HasValue ? (short)fechaEmision.Value.Month : (short)DateTime.Now.Month,
                Autorizacion = GetChildValue(infoTributaria, "claveAcceso") ?? "",
                Estab = estab,
                PtoEmi = ptoEmi,
                Secuencial = secuencial,
                // Campos de documento modificado
                TipoComprobanteModificado = tipoModificado,
                EstablecimientoModificado = estabModificado,
                PuntoEmisionModificado = ptoEmiModificado,
                SecuencialModificado = secuencialModificado,
                AutorizacionModificada = "",
                // Forma de pago
                FormaPago = formaPago,
                UsuarioCreacion = "ImportacionXML",
                FechaCreacion = DateTime.Now
            };

            SegregarBasesImponibles(compra);
            return compra;
        }

        // ---------------------------------------------------------------------
        // --- PROCESAR NOTA DÉBITO (COMPRA) ---
        // ---------------------------------------------------------------------
        private Compra? ProcesarNotaDebito(XDocument xmlDoc, List<string> resultados, string rucEmisor = "")
        {
            var infoTributaria = GetFirstDescendant(xmlDoc, "infoTributaria");
            var infoNotaDebito = GetFirstDescendant(xmlDoc, "infoNotaDebito");

            if (infoTributaria == null || infoNotaDebito == null)
            {
                resultados.Add("ADVERTENCIA: No se encontraron nodos para Nota Débito.");
                return null;
            }

            string rucProveedor = GetChildValue(infoTributaria, "ruc") ?? "";
            string razonSocialProveedor = GetChildValue(infoTributaria, "razonSocial") ?? "";
            string estab = NormalizarCodigo(GetChildValue(infoTributaria, "estab") ?? "", 3);
            string ptoEmi = NormalizarCodigo(GetChildValue(infoTributaria, "ptoEmi") ?? "", 3);
            string secuencial = NormalizarSecuencial(GetChildValue(infoTributaria, "secuencial") ?? "");
            string numComprobante = $"{estab}-{ptoEmi}-{secuencial}";

            decimal.TryParse(GetChildValue(infoNotaDebito, "totalSinImpuestos") ?? "0", System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal baseImp);
            decimal.TryParse(GetChildValue(infoNotaDebito, "importeTotal") ?? "0", System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal montoTotal);

            decimal montoIva = xmlDoc.Descendants()
                .Where(e => e.Name.LocalName.Equals("totalImpuesto", StringComparison.OrdinalIgnoreCase) && GetChildValue(e, "codigo") == "2")
                .Sum(ti => decimal.Parse(GetChildValue(ti, "valor") ?? "0", System.Globalization.CultureInfo.InvariantCulture));

            DateTime? fechaEmision = ObtenerFechaEmision(xmlDoc);

            var compra = new Compra
            {
                RucEmpresa = rucEmisor,
                CodigoCompra = $"ND{DateTime.Now.Ticks % 100000}",
                TipoComprobante = "05", // Nota Débito
                NumComprobante = numComprobante,
                IdProveedor = rucProveedor,
                RazonSocialProveedor = razonSocialProveedor,
                TipoIdProveedor = rucProveedor.Length == 13 ? "01" : "02",
                BaseImponible = baseImp,
                MontoIva = montoIva,
                MontoTotal = montoTotal,
                CodSustento = montoIva > 0 ? "01" : "02",
                FechaEmision = fechaEmision,
                FechaRegistro = fechaEmision,
                Anio = fechaEmision.HasValue ? (short)fechaEmision.Value.Year : (short)DateTime.Now.Year,
                Mes = fechaEmision.HasValue ? (short)fechaEmision.Value.Month : (short)DateTime.Now.Month,
                Autorizacion = GetChildValue(infoTributaria, "claveAcceso") ?? "",
                Estab = estab,
                PtoEmi = ptoEmi,
                Secuencial = secuencial,
                UsuarioCreacion = "ImportacionXML",
                FechaCreacion = DateTime.Now
            };

            SegregarBasesImponibles(compra);
            return compra;
        }

        // ---------------------------------------------------------------------
        // --- PROCESAR FACTURA COMO VENTA ---
        // ---------------------------------------------------------------------
        private Venta? ProcesarFacturaComoVenta(XDocument xmlDoc, List<string> resultados, string rucEmpresa)
        {
            var infoTributaria = GetFirstDescendant(xmlDoc, "infoTributaria");
            var infoFactura = GetFirstDescendant(xmlDoc, "infoFactura");

            if (infoTributaria == null || infoFactura == null) return null;

            string estab = NormalizarCodigo(GetChildValue(infoTributaria, "estab") ?? "", 3);
            string ptoEmi = NormalizarCodigo(GetChildValue(infoTributaria, "ptoEmi") ?? "", 3);
            string secuencial = NormalizarSecuencial(GetChildValue(infoTributaria, "secuencial") ?? "");
            string numComprobante = $"{estab}-{ptoEmi}-{secuencial}";

            string idCliente = GetChildValue(infoFactura, "identificacionComprador") ?? "";
            string razonCliente = GetChildValue(infoFactura, "razonSocialComprador") ?? "";

            decimal.TryParse(GetChildValue(infoFactura, "totalSinImpuestos") ?? "0", System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal baseImp);
            decimal.TryParse(GetChildValue(infoFactura, "importeTotal") ?? "0", System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal montoTotal);

            decimal montoIva = xmlDoc.Descendants()
                .Where(e => e.Name.LocalName.Equals("totalImpuesto", StringComparison.OrdinalIgnoreCase) && GetChildValue(e, "codigo") == "2")
                .Sum(ti => decimal.Parse(GetChildValue(ti, "valor") ?? "0", System.Globalization.CultureInfo.InvariantCulture));

            DateTime? fechaEmision = ObtenerFechaEmision(xmlDoc);

            // Calcular bases
            decimal baseGravada = 0, baseNoGravada = 0;
            if (montoIva > 0)
            {
                baseGravada = Math.Round(montoIva / 0.15m, 2);
                baseNoGravada = baseImp - baseGravada;
                if (baseNoGravada < 0) baseNoGravada = 0;
            }
            else
            {
                baseNoGravada = baseImp;
            }

            var venta = new Venta
            {
                RucEmpresa = rucEmpresa,
                TipoComprobante = "01", // Factura
                NumComprobante = numComprobante.Replace("-", ""),
                IdCliente = idCliente,
                RazonSocialCliente = razonCliente,
                TipoIdCliente = idCliente.Length == 13 ? "04" : idCliente.Length == 10 ? "05" : "06",
                BaseImponible = baseNoGravada,
                BaseImpGrav = baseGravada,
                MontoIva = montoIva,
                MontoTotal = montoTotal,
                FechaEmision = fechaEmision,
                Anio = fechaEmision.HasValue ? (short)fechaEmision.Value.Year : (short)DateTime.Now.Year,
                Mes = fechaEmision.HasValue ? (short)fechaEmision.Value.Month : (short)DateTime.Now.Month,
                Estab = estab,
                PtoEmi = ptoEmi,
                Secuencial = secuencial,
                FormaPago = "20",
                UsuarioCreacion = "ImportacionXML",
                FechaCreacion = DateTime.Now
            };

            return venta;
        }

        // ---------------------------------------------------------------------
        // --- PROCESAR NOTA CRÉDITO COMO VENTA ---
        // ---------------------------------------------------------------------
        private Venta? ProcesarNotaCreditoComoVenta(XDocument xmlDoc, List<string> resultados, string rucEmpresa)
        {
            var infoTributaria = GetFirstDescendant(xmlDoc, "infoTributaria");
            var infoNotaCredito = GetFirstDescendant(xmlDoc, "infoNotaCredito");

            if (infoTributaria == null || infoNotaCredito == null) return null;

            string estab = NormalizarCodigo(GetChildValue(infoTributaria, "estab") ?? "", 3);
            string ptoEmi = NormalizarCodigo(GetChildValue(infoTributaria, "ptoEmi") ?? "", 3);
            string secuencial = NormalizarSecuencial(GetChildValue(infoTributaria, "secuencial") ?? "");

            string idCliente = GetChildValue(infoNotaCredito, "identificacionComprador") ?? "";
            string razonCliente = GetChildValue(infoNotaCredito, "razonSocialComprador") ?? "";

            decimal.TryParse(GetChildValue(infoNotaCredito, "totalSinImpuestos") ?? "0", System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal baseImp);
            decimal.TryParse(GetChildValue(infoNotaCredito, "importeTotal") ?? "0", System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal montoTotal);

            decimal montoIva = xmlDoc.Descendants()
                .Where(e => e.Name.LocalName.Equals("totalImpuesto", StringComparison.OrdinalIgnoreCase) && GetChildValue(e, "codigo") == "2")
                .Sum(ti => decimal.Parse(GetChildValue(ti, "valor") ?? "0", System.Globalization.CultureInfo.InvariantCulture));

            DateTime? fechaEmision = ObtenerFechaEmision(xmlDoc);

            var venta = new Venta
            {
                RucEmpresa = rucEmpresa,
                TipoComprobante = "04", // Nota Crédito
                NumComprobante = $"{estab}{ptoEmi}{secuencial}",
                IdCliente = idCliente,
                RazonSocialCliente = razonCliente,
                TipoIdCliente = idCliente.Length == 13 ? "04" : idCliente.Length == 10 ? "05" : "06",
                BaseImponible = baseImp,
                BaseImpGrav = montoIva > 0 ? Math.Round(montoIva / 0.15m, 2) : 0,
                MontoIva = montoIva,
                MontoTotal = montoTotal,
                FechaEmision = fechaEmision,
                Anio = fechaEmision.HasValue ? (short)fechaEmision.Value.Year : (short)DateTime.Now.Year,
                Mes = fechaEmision.HasValue ? (short)fechaEmision.Value.Month : (short)DateTime.Now.Month,
                Estab = estab,
                PtoEmi = ptoEmi,
                Secuencial = secuencial,
                FormaPago = "20",
                UsuarioCreacion = "ImportacionXML",
                FechaCreacion = DateTime.Now
            };

            return venta;
        }

        // ---------------------------------------------------------------------
        // --- PROCESAR NOTA DÉBITO COMO VENTA ---
        // ---------------------------------------------------------------------
        private Venta? ProcesarNotaDebitoComoVenta(XDocument xmlDoc, List<string> resultados, string rucEmpresa)
        {
            var infoTributaria = GetFirstDescendant(xmlDoc, "infoTributaria");
            var infoNotaDebito = GetFirstDescendant(xmlDoc, "infoNotaDebito");

            if (infoTributaria == null || infoNotaDebito == null) return null;

            string estab = NormalizarCodigo(GetChildValue(infoTributaria, "estab") ?? "", 3);
            string ptoEmi = NormalizarCodigo(GetChildValue(infoTributaria, "ptoEmi") ?? "", 3);
            string secuencial = NormalizarSecuencial(GetChildValue(infoTributaria, "secuencial") ?? "");

            string idCliente = GetChildValue(infoNotaDebito, "identificacionComprador") ?? "";
            string razonCliente = GetChildValue(infoNotaDebito, "razonSocialComprador") ?? "";

            decimal.TryParse(GetChildValue(infoNotaDebito, "totalSinImpuestos") ?? "0", System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal baseImp);
            decimal.TryParse(GetChildValue(infoNotaDebito, "importeTotal") ?? "0", System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal montoTotal);

            decimal montoIva = xmlDoc.Descendants()
                .Where(e => e.Name.LocalName.Equals("totalImpuesto", StringComparison.OrdinalIgnoreCase) && GetChildValue(e, "codigo") == "2")
                .Sum(ti => decimal.Parse(GetChildValue(ti, "valor") ?? "0", System.Globalization.CultureInfo.InvariantCulture));

            DateTime? fechaEmision = ObtenerFechaEmision(xmlDoc);

            var venta = new Venta
            {
                RucEmpresa = rucEmpresa,
                TipoComprobante = "05", // Nota Débito
                NumComprobante = $"{estab}{ptoEmi}{secuencial}",
                IdCliente = idCliente,
                RazonSocialCliente = razonCliente,
                TipoIdCliente = idCliente.Length == 13 ? "04" : idCliente.Length == 10 ? "05" : "06",
                BaseImponible = baseImp,
                BaseImpGrav = montoIva > 0 ? Math.Round(montoIva / 0.15m, 2) : 0,
                MontoIva = montoIva,
                MontoTotal = montoTotal,
                FechaEmision = fechaEmision,
                Anio = fechaEmision.HasValue ? (short)fechaEmision.Value.Year : (short)DateTime.Now.Year,
                Mes = fechaEmision.HasValue ? (short)fechaEmision.Value.Month : (short)DateTime.Now.Month,
                Estab = estab,
                PtoEmi = ptoEmi,
                Secuencial = secuencial,
                FormaPago = "20",
                UsuarioCreacion = "ImportacionXML",
                FechaCreacion = DateTime.Now
            };

            return venta;
        }

        // ---------------------------------------------------------------------
        // --- PROCESAR RETENCIÓN DE COMPRA ---
        // ---------------------------------------------------------------------
        private void ProcesarRetencionCompra(XDocument xmlDoc, List<Compra> comprasNuevas, List<string> resultados, string rucEmpresa)
        {
            var infoTributaria = GetFirstDescendant(xmlDoc, "infoTributaria");
            var infoRetencion = GetFirstDescendant(xmlDoc, "infoCompRetencion");

            if (infoTributaria == null || infoRetencion == null) return;

            string estab = NormalizarCodigo(GetChildValue(infoTributaria, "estab") ?? "", 3);
            string ptoEmi = NormalizarCodigo(GetChildValue(infoTributaria, "ptoEmi") ?? "", 3);
            string secuencial = NormalizarSecuencial(GetChildValue(infoTributaria, "secuencial") ?? "");
            string numRetencion = $"{estab}-{ptoEmi}-{secuencial}";

            // El RUC del proveedor que emite la retención
            string rucEmisorRetencion = GetChildValue(infoTributaria, "ruc") ?? "";
            
            // El RUC nuestro (comprador) - está en identificacionSujetoRetenido
            string rucNuestro = GetChildValue(infoRetencion, "identificacionSujetoRetenido") ?? "";

            string claveAcceso = GetChildValue(infoTributaria, "claveAcceso") ?? "";
            DateTime? fechaEmision = ObtenerFechaEmision(xmlDoc);

            // Buscar valores de retención en el XML
            var retenciones = xmlDoc.Descendants().Where(e => e.Name.LocalName.Equals("impuesto", StringComparison.OrdinalIgnoreCase));
            decimal valRetIva = 0, valRetRenta = 0;
                
            foreach (var imp in retenciones)
            {
                string codigo = GetChildValue(imp, "codigo") ?? "";
                decimal.TryParse(GetChildValue(imp, "valorRetenido") ?? "0", System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal valorRet);
                    
                if (codigo == "1") valRetRenta = valorRet; // Renta
                else if (codigo == "2") valRetIva = valorRet; // IVA
            }

            // Crear una compra con retención
            var compra = new Compra
            {
                CodigoCompra = $"RET{DateTime.Now.Ticks % 100000}",
                TipoComprobante = "07", // Retención
                NumComprobante = numRetencion,
                IdProveedor = rucEmisorRetencion,
                RazonSocialProveedor = GetChildValue(infoTributaria, "razonSocial") ?? "",
                TipoIdProveedor = "01",
                RucEmpresa = rucEmpresa,
                BaseImponible = 0,
                MontoIva = 0,
                MontoTotal = 0,
                CodSustento = "01",
                FechaEmision = fechaEmision,
                FechaRegistro = fechaEmision,
                Anio = fechaEmision.HasValue ? (short)fechaEmision.Value.Year : (short)DateTime.Now.Year,
                Mes = fechaEmision.HasValue ? (short)fechaEmision.Value.Month : (short)DateTime.Now.Month,
                Autorizacion = claveAcceso,
                Estab = estab,
                PtoEmi = ptoEmi,
                Secuencial = secuencial,
                // Campos de retención
                ValRetServ100 = valRetIva > 0 ? valRetIva : 0,
                ValRetAir = valRetRenta > 0 ? valRetRenta : 0,
                BaseImpAir = valRetRenta > 0 ? 0 : 0,
                UsuarioCreacion = "ImportacionXML",
                FechaCreacion = DateTime.Now
            };
            comprasNuevas.Add(compra);
        }

        private static string NormalizarContextoCarga(string? contextoCarga)
        {
            return string.Equals(contextoCarga, CONTEXTO_EMITIDOS, StringComparison.OrdinalIgnoreCase)
                ? CONTEXTO_EMITIDOS
                : CONTEXTO_RECIBIDOS;
        }

        private static bool EsRecibido(string contextoCarga)
        {
            return string.Equals(contextoCarga, CONTEXTO_RECIBIDOS, StringComparison.OrdinalIgnoreCase);
        }

        private string ObtenerRucEmpresaCompra(XDocument xmlDoc, string rootName, string rucEmisor)
        {
            if (rootName == "comprobanteRetencion") return rucEmisor;

            XElement? info = rootName switch
            {
                "factura" => GetFirstDescendant(xmlDoc, "infoFactura"),
                "notacredito" => GetFirstDescendant(xmlDoc, "infoNotaCredito"),
                "notadebito" => GetFirstDescendant(xmlDoc, "infoNotaDebito"),
                _ => null
            };

            return info != null ? (GetChildValue(info, "identificacionComprador") ?? rucEmisor) : rucEmisor;
        }

        private string ObtenerRucEmpresaVenta(XDocument xmlDoc, string rootName, string rucEmisor)
        {
            if (rootName == "comprobanteRetencion")
            {
                var infoRet = GetFirstDescendant(xmlDoc, "infoCompRetencion");
                return infoRet != null ? (GetChildValue(infoRet, "identificacionSujetoRetenido") ?? rucEmisor) : rucEmisor;
            }

            return rucEmisor;
        }

        public List<string> ImportarDesdeCarpeta(string rutaCarpeta, string contextoCarga)
        {
            var resultados = new List<string>();
            contextoCarga = NormalizarContextoCarga(contextoCarga);

            if (!Directory.Exists(rutaCarpeta))
            {
                resultados.Add($"ERROR: La carpeta no existe: {rutaCarpeta}");
                return resultados;
            }

            string[] archivosXml = Directory.GetFiles(rutaCarpeta, "*.xml", SearchOption.TopDirectoryOnly);
            resultados.Add($"Archivos XML encontrados: {archivosXml.Length}");
            resultados.Add($"Contexto seleccionado: {contextoCarga}");

            var ventasNuevas = new List<Venta>();
            var ventasActualizadas = new List<Venta>();
            var comprasNuevas = new List<Compra>();
            var retencionesClientesNuevas = new List<RetencionCliente>();
            var retencionesComprasNuevas = new List<RetencionCompra>();

            DateTime fechaBaseLote = DateTime.Now;
            if (archivosXml.Length > 0)
            {
                try
                {
                    XDocument rawXmlDoc = XDocument.Load(archivosXml[0]);
                    XDocument xmlDocBase = rawXmlDoc;
                    XElement? root = rawXmlDoc.Root;

                    if (root != null && root.Name.LocalName.Equals("autorizacion", StringComparison.OrdinalIgnoreCase))
                    {
                        string comprobanteXmlString = root.Descendants()
                            .FirstOrDefault(e => e.Name.LocalName.Equals("comprobante", StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;

                        if (!string.IsNullOrWhiteSpace(comprobanteXmlString))
                        {
                            xmlDocBase = XDocument.Parse(comprobanteXmlString);
                        }
                    }

                    DateTime? fechaLoteProcesada = ObtenerFechaEmision(xmlDocBase);
                    if (fechaLoteProcesada.HasValue)
                    {
                        fechaBaseLote = fechaLoteProcesada.Value;
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

            var nuevoLote = _context.CargasLotes.FirstOrDefault(l =>
                l.Anio == anioActual &&
                l.Mes == mesActual &&
                l.TipoArchivo == tipoArchivo &&
                l.TipoDocumento == contextoCarga);

            if (nuevoLote == null)
            {
                nuevoLote = new CargaLote
                {
                    Anio = anioActual,
                    Mes = mesActual,
                    TipoArchivo = tipoArchivo,
                    TipoDocumento = contextoCarga,
                    NombreArchivo = $"Lote XML {contextoCarga} {anioActual}-{mesActual:00} - {DateTime.Now:yyyyMMddHHmmss}",
                    FechaCarga = DateTime.Now
                };
            }
            else
            {
                int loteIdExistente = nuevoLote.Id;
                _context.Entry(nuevoLote).State = EntityState.Detached;

                try
                {
                    int deletedVentas = _context.Database.ExecuteSqlRaw($"DELETE FROM Ventas WHERE CargaLoteId = {loteIdExistente}");
                    int deletedCompras = _context.Database.ExecuteSqlRaw($"DELETE FROM Compras WHERE CargaLoteId = {loteIdExistente}");
                    if (deletedVentas + deletedCompras > 0)
                    {
                        resultados.Add($"LIMPIEZA: Se eliminaron {deletedVentas + deletedCompras} registros previos del lote {loteIdExistente} ({contextoCarga}).");
                    }
                    nuevoLote.TotalRegistros = 0;
                    _context.CargasLotes.Attach(nuevoLote);
                }
                catch (Exception ex)
                {
                    resultados.Add($"❌ ERROR CRÍTICO DE BD (Limpieza de lote): {ex.InnerException?.Message ?? ex.Message}");
                    return resultados;
                }
            }

            foreach (var filePath in archivosXml)
            {
                string fileName = Path.GetFileName(filePath);
                try
                {
                    XDocument rawXmlDoc = XDocument.Load(filePath);
                    XDocument xmlDoc = rawXmlDoc;
                    XElement? root = rawXmlDoc.Root;

                    if (root != null && root.Name.LocalName.Equals("autorizacion", StringComparison.OrdinalIgnoreCase))
                    {
                        string comprobanteXmlString = root.Descendants()
                            .FirstOrDefault(e => e.Name.LocalName.Equals("comprobante", StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(comprobanteXmlString))
                        {
                            xmlDoc = XDocument.Parse(comprobanteXmlString);
                        }
                    }

                    if (xmlDoc.Root == null)
                    {
                        resultados.Add($"❌ ERROR ESTRUCTURA: No se pudo leer el XML interno ({fileName}).");
                        continue;
                    }

                    DateTime? fechaComprobanteNullable = ObtenerFechaEmision(xmlDoc);
                    if (!fechaComprobanteNullable.HasValue)
                    {
                        resultados.Add($"ADVERTENCIA FECHA: No se pudo determinar la fecha de emisión. Documento omitido ({fileName}).");
                        continue;
                    }

                    DateTime fechaComprobante = fechaComprobanteNullable.Value;
                    if (fechaComprobante.Year != anioActual || fechaComprobante.Month != mesActual)
                    {
                        resultados.Add($"ADVERTENCIA PERIODO: Comprobante de {fechaComprobante:MM/yyyy} omitido. No pertenece al lote {mesActual:00}/{anioActual}. ({fileName})");
                        continue;
                    }

                    string rootName = xmlDoc.Root.Name.LocalName.ToLowerInvariant();
                    if (rootName == "comprobanteretencion") rootName = "comprobanteRetencion";

                    var infoTributaria = GetFirstDescendant(xmlDoc, "infoTributaria");
                    string rucEmisor = GetChildValue(infoTributaria, "ruc") ?? string.Empty;

                    if (rootName == "factura")
                    {
                        if (EsRecibido(contextoCarga))
                        {
                            var compra = ProcesarFactura(xmlDoc, resultados, ObtenerRucEmpresaCompra(xmlDoc, rootName, rucEmisor));
                            if (compra != null) comprasNuevas.Add(compra);
                            resultados.Add($"OK: Factura recibida registrada en COMPRAS ({fileName})");
                        }
                        else
                        {
                            var venta = ProcesarFacturaComoVenta(xmlDoc, resultados, ObtenerRucEmpresaVenta(xmlDoc, rootName, rucEmisor));
                            if (venta != null) ventasNuevas.Add(venta);
                            resultados.Add($"OK: Factura emitida registrada en VENTAS ({fileName})");
                        }
                    }
                    else if (rootName == "notacredito")
                    {
                        if (EsRecibido(contextoCarga))
                        {
                            var compra = ProcesarNotaCredito(xmlDoc, resultados, ObtenerRucEmpresaCompra(xmlDoc, rootName, rucEmisor));
                            if (compra != null) comprasNuevas.Add(compra);
                            resultados.Add($"OK: Nota de crédito recibida registrada en COMPRAS ({fileName})");
                        }
                        else
                        {
                            var venta = ProcesarNotaCreditoComoVenta(xmlDoc, resultados, ObtenerRucEmpresaVenta(xmlDoc, rootName, rucEmisor));
                            if (venta != null) ventasNuevas.Add(venta);
                            resultados.Add($"OK: Nota de crédito emitida registrada en VENTAS ({fileName})");
                        }
                    }
                    else if (rootName == "notadebito")
                    {
                        if (EsRecibido(contextoCarga))
                        {
                            var compra = ProcesarNotaDebito(xmlDoc, resultados, ObtenerRucEmpresaCompra(xmlDoc, rootName, rucEmisor));
                            if (compra != null) comprasNuevas.Add(compra);
                            resultados.Add($"OK: Nota de débito recibida registrada en COMPRAS ({fileName})");
                        }
                        else
                        {
                            var venta = ProcesarNotaDebitoComoVenta(xmlDoc, resultados, ObtenerRucEmpresaVenta(xmlDoc, rootName, rucEmisor));
                            if (venta != null) ventasNuevas.Add(venta);
                            resultados.Add($"OK: Nota de débito emitida registrada en VENTAS ({fileName})");
                        }
                    }
                    else if (rootName == "comprobanteRetencion")
                    {
                        string rucEmpresa = EsRecibido(contextoCarga) 
                            ? ObtenerRucEmpresaVenta(xmlDoc, rootName, rucEmisor)
                            : ObtenerRucEmpresaCompra(xmlDoc, rootName, rucEmisor);
                        
                        if (EsRecibido(contextoCarga))
                        {
                            var retCliente = ProcesarRetencionClienteXML(xmlDoc, resultados, rucEmpresa);
                            if (retCliente != null) retencionesClientesNuevas.Add(retCliente);
                            resultados.Add($"OK: Retención de cliente registrada en RetencionesClientes ({fileName})");
                        }
                        else
                        {
                            var retCompra = ProcesarRetencionCompraXML(xmlDoc, resultados, rucEmpresa);
                            if (retCompra != null) retencionesComprasNuevas.Add(retCompra);
                            resultados.Add($"OK: Retención a proveedor registrada en RetencionesCompras ({fileName})");
                        }
                    }
                    else
                    {
                        resultados.Add($"ADVERTENCIA: Tipo de documento no reconocido: {rootName} ({fileName})");
                    }
                }
                catch (Exception ex)
                {
                    resultados.Add($"❌ ERROR CRÍTICO al procesar {fileName}: {ex.InnerException?.Message ?? ex.Message}");
                }
            }

            int totalRegistros = ventasNuevas.Count + ventasActualizadas.Count + comprasNuevas.Count + retencionesClientesNuevas.Count + retencionesComprasNuevas.Count;
            if (totalRegistros > 0)
            {
                if (nuevoLote.Id == 0)
                {
                    nuevoLote.TotalRegistros = totalRegistros;
                    _context.CargasLotes.Add(nuevoLote);
                    try
                    {
                        _context.SaveChanges();
                    }
                    catch (Exception ex)
                    {
                        resultados.Add($"❌ ERROR CRÍTICO DE BD (Lote padre): {ex.InnerException?.Message ?? ex.Message}");
                        return resultados;
                    }
                }
                else
                {
                    nuevoLote.TotalRegistros += totalRegistros;
                    nuevoLote.TipoDocumento = contextoCarga;
                    _context.CargasLotes.Update(nuevoLote);
                }

                int loteIdReal = nuevoLote.Id;
                foreach (var compra in comprasNuevas) compra.CargaLoteId = loteIdReal;
                foreach (var venta in ventasNuevas) venta.CargaLoteId = loteIdReal;
                foreach (var venta in ventasActualizadas) { venta.CargaLoteId = loteIdReal; _context.Ventas.Update(venta); }
                foreach (var ret in retencionesClientesNuevas) ret.CargaLoteId = loteIdReal;
                foreach (var ret in retencionesComprasNuevas) ret.CargaLoteId = loteIdReal;

                _context.Compras.AddRange(comprasNuevas);
                _context.Ventas.AddRange(ventasNuevas);
                _context.RetencionesClientes.AddRange(retencionesClientesNuevas);
                _context.RetencionesCompras.AddRange(retencionesComprasNuevas);

                try
                {
                    _context.SaveChanges();
                    resultados.Add($"✅ ÉXITO: {totalRegistros} registros guardados en lote {nuevoLote.Id} ({contextoCarga}).");
                }
                catch (Exception ex)
                {
                    resultados.Add($"❌ ERROR CRÍTICO DE BD (Registros hijos): {ex.InnerException?.Message ?? ex.Message}");
                    return resultados;
                }
            }
            else
            {
                resultados.Add("ADVERTENCIA: No se encontraron registros válidos para guardar.");
            }

            return resultados;
        }

        private static string NormalizarComprobante(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            s = s.Trim();

            if (s.Contains('-')) return s;

            var digits = new string(s.Where(char.IsDigit).ToArray());
            if (digits.Length == 15)
                return $"{digits.Substring(0, 3)}-{digits.Substring(3, 3)}-{digits.Substring(6, 9)}";

            return s;
        }

        private static string NormalizarCodigo(string valor, int digitos)
        {
            if (string.IsNullOrWhiteSpace(valor)) return new string('0', digitos);
            valor = valor.Trim();
            valor = valor.Replace("-", "");
            if (valor.Length > digitos) valor = valor.Substring(valor.Length - digitos);
            return valor.PadLeft(digitos, '0');
        }

        private static string NormalizarSecuencial(string valor)
        {
            if (string.IsNullOrWhiteSpace(valor)) return "000000001";
            valor = valor.Trim();
            valor = valor.Replace("-", "");
            var digits = new string(valor.Where(char.IsDigit).ToArray());
            return digits.PadLeft(9, '0');
        }

        private void ProcesarComprobanteRetencion(XDocument xmlDoc, List<Venta> ventasNuevas, List<Venta> ventasActualizadas, List<Compra> comprasNuevas, List<string> resultados, string rucEmpresa)
        {
            var infoTributaria = GetFirstDescendant(xmlDoc, "infoTributaria");
            var infoCompRetencion = GetFirstDescendant(xmlDoc, "infoCompRetencion");

            var impuestos = xmlDoc.Descendants().Where(e => e.Name.LocalName.Equals("impuesto", StringComparison.OrdinalIgnoreCase));

            if (infoTributaria == null || infoCompRetencion == null)
            {
                resultados.Add($"ADVERTENCIA PARSEO: No se encontraron los nodos infoTributaria/infoCompRetencion. Archivo omitido.");
                return;
            }

            string estab = NormalizarCodigo(GetChildValue(infoTributaria, "estab") ?? "", 3);
            string ptoEmi = NormalizarCodigo(GetChildValue(infoTributaria, "ptoEmi") ?? "", 3);
            string secuencial = NormalizarSecuencial(GetChildValue(infoTributaria, "secuencial") ?? "");
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
                .FirstOrDefault(v => v.NumComprobante == numComprobanteSustento && v.IdCliente == rucCliente && v.RucEmpresa == rucEmpresa);

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

                ventaExistente.RucEmpresa = rucEmpresa;
                ventaExistente.AutorizacionRetencion = GetChildValue(infoTributaria, "claveAcceso") ?? "";
                ventaExistente.FechaRetencion = fechaRetencion; // ✅ Ahora esta variable existe
                ventaExistente.RazonSocialCliente = razonSocialCliente;

                ventasActualizadas.Add(ventaExistente);
                resultados.Add($"RET VENTA: Actualizada para {numComprobanteSustento} y agregada a la lista de guardado.");
            }
            else
            {
                var nuevaVentaCero = CrearVentaCeroRetencion(infoTributaria, infoCompRetencion, impuestos, totalRetIVA, totalRetRenta, razonSocialCliente, estab, ptoEmi, secuencial, mesRet, anioRet, fechaRetencion, rucEmpresa); // ✅ Ahora se pasa correctamente
                ventasNuevas.Add(nuevaVentaCero);
                resultados.Add($"ADVERTENCIA RET VENTA: La factura de venta {numComprobanteSustento} no se encontró. Se CREÓ VENTA CERO para registrar la retención.");
            }
        }

        private Venta CrearVentaCeroRetencion(
    XElement infoTributaria,
    XElement infoCompRetencion,
    IEnumerable<XElement> impuestos,
    decimal totalRetIVA,
    decimal totalRetRenta,
    string razonSocialCliente,
    string estab,
    string ptoEmi,
    string secuencial,
    short mesRet,
    short anioRet,
    DateTime? fechaRetencion,
    string rucEmpresa)
        {
            string numRetencionUnformatted = estab + ptoEmi + secuencial;

            DateTime? fechaEmisionDoc = null;
            string fechaDocStr = GetChildValue(infoCompRetencion, "fechaEmisionDocSustento") ?? "";
            if (DateTime.TryParseExact(fechaDocStr, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var f))
                fechaEmisionDoc = f;

            // Extraer RUC del emisor (empresa que emite la retención)
            string rucEmisorRetencion = GetChildValue(infoTributaria, "ruc") ?? "";

            return new Venta
            {
                RucEmpresa = rucEmpresa,
                valRetRenta = totalRetRenta,
                valRetIVA = totalRetIVA,

                MontoTotal = 0.00m,
                BaseImponible = 0.00m,
                MontoIva = 0.00m,

                NumRetencion = numRetencionUnformatted,
                AutorizacionRetencion = GetChildValue(infoTributaria, "claveAcceso") ?? "",
                FechaRetencion = fechaRetencion,

                Anio = anioRet,
                Mes = mesRet,

                Estab = estab,
                PtoEmi = ptoEmi,
                Secuencial = secuencial,

                TipoComprobante = GetChildValue(infoCompRetencion, "codDocSustento") ?? "01",
                NumComprobante = NormalizarComprobante(
                    GetChildValue(infoCompRetencion, "numDocSustento")
                ),

                // 🔥 FIX CLAVE
                FechaEmision = fechaEmisionDoc ?? fechaRetencion ?? DateTime.Now.Date,

                IdCliente = GetChildValue(infoCompRetencion, "identificacionSujetoRetenido") ?? "",
                RazonSocialCliente = razonSocialCliente,

                UsuarioCreacion = "CargaRetencionXMLCero",
                FechaCreacion = DateTime.Now
            };
        }


        private Compra CrearEntidadCompraRetencion(XElement infoTributaria, XElement infoCompRetencion, IEnumerable<XElement> impuestos, string rucProveedor, string claveAcceso, DateTime fechaEmisionDoc, string numRetencion)
        {
            // ... (código original, ajustado para seguridad de cadenas)
            string estab = NormalizarCodigo(GetChildValue(infoTributaria, "estab") ?? "", 3);
            string ptoEmi = NormalizarCodigo(GetChildValue(infoTributaria, "ptoEmi") ?? "", 3);
            string secuencial = NormalizarSecuencial(GetChildValue(infoTributaria, "secuencial") ?? "");

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

        private RetencionCliente? ProcesarRetencionClienteXML(XDocument xmlDoc, List<string> resultados, string rucEmpresa)
        {
            try
            {
                var infoTributaria = GetFirstDescendant(xmlDoc, "infoTributaria");
                var infoCompRetencion = GetFirstDescendant(xmlDoc, "infoCompRetencion");

                if (infoTributaria == null || infoCompRetencion == null)
                {
                    resultados.Add("ADVERTENCIA: No se encontró infoTributaria o infoCompRetencion en el XML.");
                    return null;
                }

                string estab = NormalizarCodigo(GetChildValue(infoTributaria, "estab") ?? "", 3);
                string ptoEmi = GetChildValue(infoTributaria, "ptoEmi") ?? "001";
                string secuencial = GetChildValue(infoTributaria, "secuencial") ?? "000000001";
                string numRetencionCompleto = $"{estab}-{ptoEmi}-{secuencial}";

                var fechaRetencion = ObtenerFechaEmision(xmlDoc);

                string docAfectado = GetChildValue(infoCompRetencion, "numDocSustento") ?? "";
                DateTime? fechaDocAfectado = null;
                string fechaDocStr = GetChildValue(infoCompRetencion, "fechaEmisionDocSustento") ?? "";
                if (!string.IsNullOrEmpty(fechaDocStr) && DateTime.TryParseExact(fechaDocStr, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var f))
                    fechaDocAfectado = f;

                var impuestos = xmlDoc.Descendants().Where(e => e.Name.LocalName.Equals("impuesto", StringComparison.OrdinalIgnoreCase));

                decimal baseImpGrav = 0;
                decimal baseImpAir = 0;
                decimal valRetIva = 0;
                decimal valRetRenta = 0;
                decimal porcentajeAir = 0;

                foreach (var imp in impuestos)
                {
                    string codigo = GetChildValue(imp, "codigo") ?? "";
                    string codigoRet = GetChildValue(imp, "codigoRetencion") ?? "";
                    decimal valorRetenido = decimal.Parse(GetChildValue(imp, "valorRetenido") ?? "0", CultureInfo.InvariantCulture);
                    decimal baseImponible = decimal.Parse(GetChildValue(imp, "baseImponible") ?? "0", CultureInfo.InvariantCulture);
                    
                    if (codigo == "2" && !string.IsNullOrEmpty(codigoRet))
                    {
                        baseImpGrav += baseImponible;
                        valRetIva += valorRetenido;
                    }
                    else if (codigo == "1" && !string.IsNullOrEmpty(codigoRet))
                    {
                        baseImpAir += baseImponible;
                        valRetRenta += valorRetenido;
                        if (baseImponible > 0)
                            porcentajeAir = Math.Round((valorRetenido / baseImponible) * 100, 2);
                    }
                }

                var periodoFiscal = GetChildValue(infoCompRetencion, "periodoFiscal") ?? "";
                short anio = (short)DateTime.Now.Year;
                short mes = (short)DateTime.Now.Month;
                if (!string.IsNullOrEmpty(periodoFiscal) && periodoFiscal.Contains('/'))
                {
                    var parts = periodoFiscal.Split('/');
                    if (parts.Length == 2)
                    {
                        short.TryParse(parts[0], out mes);
                        short.TryParse(parts[1], out anio);
                    }
                }

                return new RetencionCliente
                {
                    RucEmpresa = rucEmpresa,
                    IdCliente = GetChildValue(infoCompRetencion, "identificacionSujetoRetenido") ?? "",
                    RazonSocialCliente = GetChildValue(infoCompRetencion, "razonSocialSujetoRetenido") ?? "",
                    DocAfectado = docAfectado,
                    FechaDocAfectado = fechaDocAfectado,
                    NumRetencionCompleto = numRetencionCompleto,
                    NumRetencion = estab + ptoEmi + secuencial,
                    AutorizacionRetencion = GetChildValue(infoTributaria, "claveAcceso") ?? "",
                    FechaRetencion = fechaRetencion,
                    BaseImpGrav = baseImpGrav,
                    MontoIva = 0,
                    BaseImpAir = baseImpAir,
                    PorcentajeAir = porcentajeAir,
                    ValRetIva = valRetIva,
                    ValRetRenta = valRetRenta,
                    TotalRetencion = valRetIva + valRetRenta,
                    Anio = anio,
                    Mes = mes,
                    UsuarioCreacion = "ImportadorXML",
                    FechaCreacion = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                resultados.Add($"ERROR al procesar retención cliente: {ex.Message}");
                return null;
            }
        }

        private RetencionCompra? ProcesarRetencionCompraXML(XDocument xmlDoc, List<string> resultados, string rucEmpresa)
        {
            try
            {
                var infoTributaria = GetFirstDescendant(xmlDoc, "infoTributaria");
                var infoCompRetencion = GetFirstDescendant(xmlDoc, "infoCompRetencion");

                if (infoTributaria == null || infoCompRetencion == null)
                {
                    resultados.Add("ADVERTENCIA: No se encontró infoTributaria o infoCompRetencion en el XML.");
                    return null;
                }

                string estab = NormalizarCodigo(GetChildValue(infoTributaria, "estab") ?? "", 3);
                string ptoEmi = GetChildValue(infoTributaria, "ptoEmi") ?? "001";
                string secuencial = GetChildValue(infoTributaria, "secuencial") ?? "000000001";
                string numRetencionCompleto = $"{estab}-{ptoEmi}-{secuencial}";

                var fechaRetencion = ObtenerFechaEmision(xmlDoc);

                string docAfectado = GetChildValue(infoCompRetencion, "numDocSustento") ?? "";
                DateTime? fechaDocAfectado = null;
                string fechaDocStr = GetChildValue(infoCompRetencion, "fechaEmisionDocSustento") ?? "";
                if (!string.IsNullOrEmpty(fechaDocStr) && DateTime.TryParseExact(fechaDocStr, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var f))
                    fechaDocAfectado = f;

                var impuestos = xmlDoc.Descendants().Where(e => e.Name.LocalName.Equals("impuesto", StringComparison.OrdinalIgnoreCase));

                decimal baseImpGrav = 0;
                decimal baseImpAir = 0;
                decimal valRetIva = 0;
                decimal valRetRenta = 0;
                decimal porcentajeAir = 0;

                foreach (var imp in impuestos)
                {
                    string codigo = GetChildValue(imp, "codigo") ?? "";
                    string codigoRet = GetChildValue(imp, "codigoRetencion") ?? "";
                    decimal valorRetenido = decimal.Parse(GetChildValue(imp, "valorRetenido") ?? "0", CultureInfo.InvariantCulture);
                    decimal baseImponible = decimal.Parse(GetChildValue(imp, "baseImponible") ?? "0", CultureInfo.InvariantCulture);
                    
                    if (codigo == "2" && !string.IsNullOrEmpty(codigoRet))
                    {
                        baseImpGrav += baseImponible;
                        valRetIva += valorRetenido;
                    }
                    else if (codigo == "1" && !string.IsNullOrEmpty(codigoRet))
                    {
                        baseImpAir += baseImponible;
                        valRetRenta += valorRetenido;
                        if (baseImponible > 0)
                            porcentajeAir = Math.Round((valorRetenido / baseImponible) * 100, 2);
                    }
                }

                var periodoFiscal = GetChildValue(infoCompRetencion, "periodoFiscal") ?? "";
                short anio = (short)DateTime.Now.Year;
                short mes = (short)DateTime.Now.Month;
                if (!string.IsNullOrEmpty(periodoFiscal) && periodoFiscal.Contains('/'))
                {
                    var parts = periodoFiscal.Split('/');
                    if (parts.Length == 2)
                    {
                        short.TryParse(parts[0], out mes);
                        short.TryParse(parts[1], out anio);
                    }
                }

                return new RetencionCompra
                {
                    RucEmpresa = rucEmpresa,
                    IdProveedor = GetChildValue(infoCompRetencion, "identificacionSujetoRetenido") ?? "",
                    RazonSocialProveedor = GetChildValue(infoCompRetencion, "razonSocialSujetoRetenido") ?? "",
                    DocAfectado = docAfectado,
                    FechaDocAfectado = fechaDocAfectado,
                    NumRetencionCompleto = numRetencionCompleto,
                    NumRetencion = estab + ptoEmi + secuencial,
                    Autorizacion = GetChildValue(infoTributaria, "claveAcceso") ?? "",
                    FechaRetencion = fechaRetencion,
                    BaseImpGrav = baseImpGrav,
                    MontoIva = 0,
                    BaseImpAir = baseImpAir,
                    PorcentajeAir = porcentajeAir,
                    ValRetIva = valRetIva,
                    ValRetRenta = valRetRenta,
                    TotalRetencion = valRetIva + valRetRenta,
                    Anio = anio,
                    Mes = mes,
                    UsuarioCreacion = "ImportadorXML",
                    FechaCreacion = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                resultados.Add($"ERROR al procesar retención compra: {ex.Message}");
                return null;
            }
        }
    }
}