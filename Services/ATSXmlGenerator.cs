using AtsManager.Pages.Empresas.Models;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace AtsManager.Services
{
    public class ATSXmlGenerator
    {
        private readonly string _ruc;
        private readonly string _razonSocial;
        private const decimal UMBRAL_PAGO_OBLIGATORIO = 500.00M;

        public string Ruc => _ruc;

        public ATSXmlGenerator(string ruc, string razonSocial)
        {

        }

        private (string estab, string ptoEmi, string secuencial) ExtraerComponentesComprobante(string numComprobante)
        {
            if (string.IsNullOrWhiteSpace(numComprobante))
                return ("001", "001", "000000001");

            string limpio = numComprobante.Replace("-", "").Trim();
            var digitos = new string(limpio.Where(char.IsDigit).ToArray());

            if (digitos.Length >= 15)
            {
                string estab = digitos.Substring(0, 3);
                string ptoEmi = digitos.Substring(3, 3);
                string secuencial = digitos.Substring(6, 9);
                return (estab, ptoEmi, secuencial);
            }
            else if (digitos.Length >= 9)
            {
                string estab = digitos.Substring(0, Math.Min(3, digitos.Length));
                string ptoEmi = digitos.Length >= 6 ? digitos.Substring(3, Math.Min(3, digitos.Length - 3)) : "001";
                string secuencial = digitos.Length > 6 ? digitos.Substring(6) : "000000001";
                return (estab.PadLeft(3, '0'), ptoEmi.PadLeft(3, '0'), secuencial.PadLeft(9, '0'));
            }

            return ("001", "001", "000000001");
        }

        private string CleanStringForXml(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            string normalized = input.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (char c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }
            string cleaned = stringBuilder.ToString().Normalize(NormalizationForm.FormC);

            cleaned = cleaned.Replace(".", "").Replace(",", "").Replace('\u00A0', ' ').Trim();

            stringBuilder.Clear();
            foreach (char c in cleaned)
            {
                if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
                {
                    stringBuilder.Append(c);
                }
            }
            return stringBuilder.ToString().Trim().ToUpperInvariant();
        }

        private string SanitizeForXml(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            var result = new StringBuilder();
            foreach (char c in input)
            {
                if (c == 0x9 || c == 0xA || c == 0xD ||
                    (c >= 0x20 && c <= 0xD7FF) ||
                    (c >= 0xE000 && c <= 0xFFFD) ||
                    (c >= 0x10000 && c <= 0x10FFFF))
                {
                    result.Append(c);
                }
            }

            string cleaned = result.ToString();
            return cleaned
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }

        private string CleanNumericForXml(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            return SanitizeForXml(new string(input.Where(char.IsDigit).ToArray()));
        }

        private string EscapeXml(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            return input
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }

        public byte[] GenerarXmlBytes(
            int mes,
            int anio,
            List<Compra> compras,
            List<Venta> ventas,
            string? rucOverride = null,
            string? razonSocialOverride = null
        )
        {
            var xmlCulture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            xmlCulture.NumberFormat.NumberDecimalSeparator = ".";

            string rucFinal = !string.IsNullOrWhiteSpace(rucOverride) ? rucOverride 
                : (!string.IsNullOrWhiteSpace(_ruc) ? _ruc : "0000000000001");
            string razonSocialFinal = !string.IsNullOrWhiteSpace(razonSocialOverride)
                ? razonSocialOverride
                : (!string.IsNullOrWhiteSpace(_razonSocial) ? _razonSocial : "CONTRIBUYENTE");

            string razonSocialLimpia = CleanStringForXml(razonSocialFinal);

            var safeVentas = ventas ?? new List<Venta>();
            var safeCompras = compras ?? new List<Compra>();

            var filteredVentas = safeVentas;
            var filteredCompras = safeCompras;

            if (!string.IsNullOrWhiteSpace(rucFinal))
            {
                filteredVentas = safeVentas.Where(v => v.RucEmpresa == rucFinal).ToList();
                filteredCompras = safeCompras.Where(c => c.RucEmpresa == rucFinal).ToList();
            }

            var groupedVentas = filteredVentas
                .Where(v => v.IdCliente != "9999999999999" || v.TipoComprobante == "04" || v.TipoComprobante == "05")
                .GroupBy(v => new { v.TipoIdCliente, v.IdCliente, v.TipoComprobante })
                .Select(g => new VentaAgrupada
                {
                    TipoIdCliente = g.Key.TipoIdCliente ?? "",
                    IdCliente = g.Key.IdCliente ?? "",
                    TipoComprobante = g.Key.TipoComprobante ?? "",
                    BaseNoGraIva = g.Sum(x => x.BaseNoGraIva) ?? 0,
                    BaseImponible = g.Sum(x => x.BaseImponible) ?? 0,
                    BaseImpGrav = g.Sum(x => x.BaseImpGrav) ?? 0,
                    MontoIva = g.Sum(x => x.MontoIva) ?? 0,
                    MontoIce = g.Sum(x => x.MontoIce) ?? 0,
                    ValRetIva = g.Sum(x => x.valRetIVA) ?? 0,
                    ValRetRenta = g.Sum(x => x.valRetRenta) ?? 0,
                    MontoTotal = g.Sum(x => x.MontoTotal) ?? 0,
                    RazonSocialCliente = g.First().RazonSocialCliente ?? "",
                    FormaPago = g.First().FormaPago ?? "20",
                    NumeroComprobantes = g.Count()
                })
                .ToList();

            decimal totalVentas = filteredVentas
                .Where(v => v.IdCliente != "9999999999999" || v.TipoComprobante == "04" || v.TipoComprobante == "05")
                .Sum(v => Math.Abs(v.BaseImponible ?? 0) + Math.Abs(v.BaseImpGrav ?? 0));

            XElement raiz = new XElement("iva",

                new XElement("TipoIDInformante", rucFinal.Length == 13 ? "R" : "C"),
                new XElement("IdInformante", rucFinal),
                new XElement("razonSocial", razonSocialLimpia),
                new XElement("Anio", anio),
                new XElement("Mes", mes.ToString("D2")),

                new XElement("numEstabRuc", "001"),
                new XElement("totalVentas", totalVentas.ToString("F2", xmlCulture)),
                new XElement("codigoOperativo", "IVA"),

                GenerarDetallesCompras(filteredCompras, xmlCulture, mes, anio),
                GenerarDetallesVentas(groupedVentas, xmlCulture),
                GenerarVentasEstablecimiento(filteredVentas, xmlCulture)
            );

            XDocument document = new XDocument(
                new XDeclaration("1.0", "UTF-8", "yes"),
                raiz
            );

            using (var memoryStream = new MemoryStream())
            {
                using (var writer = new StreamWriter(memoryStream, new UTF8Encoding(false)))
                {
                    document.Save(writer, SaveOptions.DisableFormatting);
                }
                return memoryStream.ToArray();
            }
        }

        public string GenerarXmlString(
            int mes,
            int anio,
            List<Compra> compras,
            List<Venta> ventas,
            List<RetencionCliente>? retenciones = null,
            string? rucOverride = null,
            string? razonSocialOverride = null
        )
        {
            var xmlCulture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            xmlCulture.NumberFormat.NumberDecimalSeparator = ".";

            string rucFinal = !string.IsNullOrWhiteSpace(rucOverride) ? rucOverride 
                : (!string.IsNullOrWhiteSpace(_ruc) ? _ruc : "0000000000001");
            string razonSocialFinal = !string.IsNullOrWhiteSpace(razonSocialOverride)
                ? razonSocialOverride
                : (!string.IsNullOrWhiteSpace(_razonSocial) ? _razonSocial : "CONTRIBUYENTE");

            string razonSocialLimpia = CleanStringForXml(razonSocialFinal);

            var safeVentas = ventas ?? new List<Venta>();
            var safeCompras = compras ?? new List<Compra>();
            var safeRetenciones = retenciones ?? new List<RetencionCliente>();

            var filteredVentas = safeVentas;
            var filteredCompras = safeCompras;
            var filteredRetenciones = safeRetenciones;

            if (!string.IsNullOrWhiteSpace(rucFinal))
            {
                filteredVentas = safeVentas.Where(v => v.RucEmpresa == rucFinal).ToList();
                filteredCompras = safeCompras.Where(c => c.RucEmpresa == rucFinal).ToList();
                filteredRetenciones = safeRetenciones.Where(r => r.RucEmpresa == rucFinal).ToList();
            }

            var groupedVentas = filteredVentas
                .Where(v => v.IdCliente != "9999999999999" || v.TipoComprobante == "04" || v.TipoComprobante == "05")
                .GroupBy(v => new { v.TipoIdCliente, v.IdCliente, v.TipoComprobante })
                .Select(g => new VentaAgrupada
                {
                    TipoIdCliente = g.Key.TipoIdCliente ?? "",
                    IdCliente = g.Key.IdCliente ?? "",
                    TipoComprobante = g.Key.TipoComprobante ?? "",
                    BaseNoGraIva = g.Sum(x => x.BaseNoGraIva) ?? 0,
                    BaseImponible = g.Sum(x => x.BaseImponible) ?? 0,
                    BaseImpGrav = g.Sum(x => x.BaseImpGrav) ?? 0,
                    MontoIva = g.Sum(x => x.MontoIva) ?? 0,
                    MontoIce = g.Sum(x => x.MontoIce) ?? 0,
                    ValRetIva = g.Sum(x => x.valRetIVA) ?? 0,
                    ValRetRenta = g.Sum(x => x.valRetRenta) ?? 0,
                    MontoTotal = g.Sum(x => x.MontoTotal) ?? 0,
                    RazonSocialCliente = g.First().RazonSocialCliente ?? "",
                    FormaPago = g.First().FormaPago ?? "20",
                    NumeroComprobantes = g.Count()
                })
                .ToList();

            decimal totalVentas = filteredVentas
                .Where(v => v.IdCliente != "9999999999999" || v.TipoComprobante == "04" || v.TipoComprobante == "05")
                .Sum(v => Math.Abs(v.BaseImponible ?? 0) + Math.Abs(v.BaseImpGrav ?? 0));

            // Convert retenciones (tipoComprobante=07) to ventas with 0 bases but retention values
            // Group by client to avoid duplicate entries for the same client
            var retencionesAgrupadas = filteredRetenciones
                .GroupBy(r => r.RucEmisor)
                .Select(g => new {
                    RucEmisor = g.Key,
                    RazonSocialEmisor = g.First().RazonSocialEmisor,
                    ValRetIva = g.Sum(x => x.ValRetIva ?? 0),
                    ValRetRenta = g.Sum(x => x.ValRetRenta ?? 0)
                })
                .ToList();

            // Merge retenciones INTO existing grouped ventas for the same client
            foreach (var ret in retencionesAgrupadas)
            {
                var ventaExistente = groupedVentas.FirstOrDefault(v => v.IdCliente == ret.RucEmisor);
                if (ventaExistente != null)
                {
                    ventaExistente.ValRetIva += ret.ValRetIva;
                    ventaExistente.ValRetRenta += ret.ValRetRenta;
                }
                else
                {
                    groupedVentas.Add(new VentaAgrupada
                    {
                        TipoIdCliente = ret.RucEmisor?.Length == 13 ? "04" : ret.RucEmisor?.Length == 10 ? "05" : "06",
                        IdCliente = ret.RucEmisor ?? "",
                        TipoComprobante = "18",
                        ValRetIva = ret.ValRetIva,
                        ValRetRenta = ret.ValRetRenta,
                        RazonSocialCliente = ret.RazonSocialEmisor ?? "",
                        FormaPago = "20",
                        NumeroComprobantes = 1
                    });
                }
            }

            // No need to concat - retenciones are now merged into groupedVentas
            var allVentas = groupedVentas;

            XElement raiz = new XElement("iva",

                new XElement("TipoIDInformante", rucFinal.Length == 13 ? "R" : "C"),
                new XElement("IdInformante", rucFinal),
                new XElement("razonSocial", razonSocialLimpia),
                new XElement("Anio", anio),
                new XElement("Mes", mes.ToString("D2")),

                new XElement("numEstabRuc", "001"),
                new XElement("totalVentas", totalVentas.ToString("F2", xmlCulture)),
                new XElement("codigoOperativo", "IVA"),

                GenerarDetallesCompras(filteredCompras, xmlCulture, mes, anio),
                GenerarDetallesVentas(allVentas, xmlCulture),
                GenerarVentasEstablecimiento(filteredVentas, xmlCulture)
            );

            XDocument document = new XDocument(
                new XDeclaration("1.0", "UTF-8", "yes"),
                raiz
            );

            using (var stringWriter = new Utf8StringWriter())
            {
                document.Save(stringWriter, SaveOptions.None);
                return stringWriter.ToString();
            }
        }

        private class Utf8StringWriter : StringWriter
        {
            public override Encoding Encoding => Encoding.UTF8;
        }

        private class VentaAgrupada
        {
            public string TipoIdCliente { get; set; } = "";
            public string IdCliente { get; set; } = "";
            public string TipoComprobante { get; set; } = "";
            public decimal BaseNoGraIva { get; set; }
            public decimal BaseImponible { get; set; }
            public decimal BaseImpGrav { get; set; }
            public decimal MontoIva { get; set; }
            public decimal MontoIce { get; set; }
            public decimal ValRetIva { get; set; }
            public decimal ValRetRenta { get; set; }
            public decimal MontoTotal { get; set; }
            public string RazonSocialCliente { get; set; } = "";
            public string FormaPago { get; set; } = "";
            public int NumeroComprobantes { get; set; }
        }

        private XElement GenerarDetallesCompras(IEnumerable<Compra> compras, CultureInfo xmlCulture, int mes, int anio)
        {
            XElement nodoCompras = new XElement("compras");

            foreach (var compra in compras)
            {
                decimal montoTotalCompra = compra.MontoTotal ?? 0.00M;
                bool requiereFormaPago = montoTotalCompra >= UMBRAL_PAGO_OBLIGATORIO;

                string numComprobante = compra.NumComprobante ?? string.Empty;
                var (establecimiento, puntoEmision, secuencial) = ExtraerComponentesComprobante(numComprobante);

                string denoProvLimpia = EscapeXml(CleanStringForXml(compra.RazonSocialProveedor ?? string.Empty));

                string codSustento = EscapeXml(SanitizeForXml(compra.CodSustento ?? ""));
                string tpIdProv = EscapeXml(SanitizeForXml(compra.TipoIdProveedor ?? ""));
                string idProv = CleanNumericForXml(compra.IdProveedor ?? "");
                string tipoComprobante = EscapeXml(SanitizeForXml(compra.TipoComprobante ?? ""));
                string tipoProv = EscapeXml(SanitizeForXml(compra.TipoProveedor ?? ""));
                string parteRel = compra.ParteRelacionada ? "SI" : "NO";
                string autorizacion = CleanNumericForXml(compra.Autorizacion ?? "");
                string formaPago = CleanNumericForXml(compra.FormaPago ?? "01");

                DateTime fechaRegistro = compra.FechaRegistro.GetValueOrDefault();
                DateTime fechaEmision = compra.FechaEmision.GetValueOrDefault();

                if (fechaRegistro.Month != mes || fechaRegistro.Year != anio)
                {
                    fechaRegistro = fechaEmision;
                }

                // Verificar si hay datos AIR reales
                bool tieneAir = compra.BaseImpAir.HasValue && compra.BaseImpAir.Value > 0;

                // Handle IVA=0: when montoIva is 0, amount should go to baseImponible (tarifa 0%), not baseNoGraIva (no objeto IVA)
                decimal baseNoGraIvaValor = compra.BaseNoGraIva ?? 0;
                decimal baseImponibleValor = compra.BaseImponible ?? 0;
                decimal baseImpGravValor = compra.BaseImpGrav ?? 0;

                if ((compra.MontoIva ?? 0) == 0)
                {
                    // If IVA is 0, move baseNoGraIva to baseImponible (tarifa 0%)
                    baseImponibleValor = baseImponibleValor + baseNoGraIvaValor;
                    baseNoGraIvaValor = 0;
                }

                var elementosCompra = new List<XElement>
                {
                    new XElement("codSustento", codSustento),
                    new XElement("tpIdProv", tpIdProv),
                    new XElement("idProv", idProv),
                    new XElement("tipoComprobante", tipoComprobante),
                    new XElement("tipoProv", tipoProv),
                    new XElement("denoProv", denoProvLimpia),
                    new XElement("parteRel", parteRel),
                    new XElement("fechaRegistro", fechaRegistro.ToString("dd/MM/yyyy", xmlCulture)),
                    new XElement("establecimiento", establecimiento),
                    new XElement("puntoEmision", puntoEmision),
                    new XElement("secuencial", secuencial),
                    new XElement("fechaEmision", fechaEmision.ToString("dd/MM/yyyy", xmlCulture)),
                    new XElement("autorizacion", autorizacion),
                    new XElement("baseNoGraIva", Math.Abs(baseNoGraIvaValor).ToString("F2", xmlCulture)),
                    new XElement("baseImponible", Math.Abs(baseImponibleValor).ToString("F2", xmlCulture)),
                    new XElement("baseImpGrav", Math.Abs(baseImpGravValor).ToString("F2", xmlCulture)),
                    new XElement("baseImpExe", Math.Abs(compra.BaseImpExe ?? 0.00M).ToString("F2", xmlCulture)),
                    new XElement("montoIce", Math.Abs(compra.MontoIce ?? 0.00M).ToString("F2", xmlCulture)),
                    new XElement("montoIva", Math.Abs(compra.MontoIva ?? 0.00M).ToString("F2", xmlCulture)),
                    new XElement("valRetBien10", Math.Abs(compra.ValRetBien10 ?? 0.00M).ToString("F2", xmlCulture)),
                    new XElement("valRetServ20", Math.Abs(compra.ValRetServ20 ?? 0.00M).ToString("F2", xmlCulture)),
                    new XElement("valorRetBienes", Math.Abs(compra.ValorRetBienes ?? 0.00M).ToString("F2", xmlCulture)),
                    new XElement("valRetServ50", Math.Abs(compra.ValRetServ50 ?? 0.00M).ToString("F2", xmlCulture)),
                    new XElement("valorRetServicios", Math.Abs(compra.ValorRetServicios ?? 0.00M).ToString("F2", xmlCulture)),
                    new XElement("valRetServ100", Math.Abs(compra.ValRetServ100 ?? 0.00M).ToString("F2", xmlCulture)),
                    new XElement("valorRetencionNc", Math.Abs(compra.ValorRetencionNc ?? 0.00M).ToString("F2", xmlCulture)),
                    new XElement("totbasesImpReemb", "0.00"),
                    new XElement("pagoExterior",
                        new XElement("pagoLocExt", EscapeXml(SanitizeForXml(compra.PagoLocExt ?? "01"))),
                        new XElement("paisEfecPago", "NA"),
                        new XElement("aplicConvDobTrib", "NA"),
                        new XElement("pagExtSujRetNorLeg", "NA")
                    )
                };

                if (requiereFormaPago)
                {
                    elementosCompra.Add(new XElement("formasDePago", new XElement("formaPago", formaPago)));
                }

                // docModificado solo para Notas de Crédito (04) y Notas de Débito (05)
                if (compra.TipoComprobante == "04" || compra.TipoComprobante == "05")
                {
                    var camposModificados = BuildDocModificadoSimple(compra);
                    if (camposModificados != null)
                    {
                        foreach (var campo in camposModificados)
                        {
                            elementosCompra.Add(campo);
                        }
                    }
                }

                // ORDEN ATS: reembolsos va ANTES de air
                // Agregar reembolsos solo si hay datos (por ahora vacío = no agregar)
                // El totbasesImpReemb ya está en 0.00, no agregamos nodo reembolsos vacío
                
                // Solo agregar air si tiene datos reales
                if (tieneAir)
                {
                    XElement detalleAir = new XElement("air");
                    XElement subDetalleAir = new XElement("detalleAir",
                            new XElement("codRetAir", compra.CodRetAir ?? "332"),
                            new XElement("baseImpAir", (compra.BaseImpAir ?? 0.00M).ToString("F2", xmlCulture)),
                            new XElement("porcentajeAir", (compra.PorcentajeAir ?? 0.00M).ToString("F2", xmlCulture)),
                            new XElement("valRetAir", (compra.ValRetAir ?? 0.00M).ToString("F2", xmlCulture))
                        );
                    detalleAir.Add(subDetalleAir);
                    elementosCompra.Add(detalleAir);
                }

                XElement detalleCompra = new XElement("detalleCompras", elementosCompra);
                nodoCompras.Add(detalleCompra);
            }
            return nodoCompras;
        }

        // Builds simple docModificado fields for NC (04) and ND (05) - ATS schema expects flat elements
        internal List<XElement>? BuildDocModificadoSimple(Compra compra)
        {
            if (compra == null || (compra.TipoComprobante != "04" && compra.TipoComprobante != "05"))
            {
                return null;
            }

            string tipoComp = compra.TipoComprobante ?? "";
            if (tipoComp != "04" && tipoComp != "05")
            {
                return null;
            }

            // Obtener datos del documento modificado
            string authOriginal = compra.AutorizacionModificada ?? "";
            string authDigits = new string(authOriginal.Where(char.IsDigit).ToArray());

            // Si no tiene autorización válida (37+ dígitos), usar 37 nueves
            if (authDigits.Length < 37)
            {
                authDigits = new string('9', 37);
                LogDocModificado($"BuildDocModificadoSimple (Compra): Sin autorización válida, usando 37 nueves");
            }

            // docModificado - tipo de comprobante afectado (default 01 = factura)
            string docMod = "01";
            if (!string.IsNullOrWhiteSpace(compra.TipoComprobanteModificado) && compra.TipoComprobanteModificado.Length >= 2)
            {
                docMod = compra.TipoComprobanteModificado.Substring(0, 2);
            }

            // estabModificado - 3 digits
            string estabMod = "001";
            if (!string.IsNullOrWhiteSpace(compra.EstablecimientoModificado) && compra.EstablecimientoModificado.Length >= 3)
            {
                estabMod = compra.EstablecimientoModificado.Substring(0, 3);
            }

            // ptoEmiModificado - 3 digits
            string ptoEmiMod = "001";
            if (!string.IsNullOrWhiteSpace(compra.PuntoEmisionModificado) && compra.PuntoEmisionModificado.Length >= 3)
            {
                ptoEmiMod = compra.PuntoEmisionModificado.Substring(0, 3);
            }

            // secModificado - pad to 9 digits
            string secMod = "000000001";
            if (!string.IsNullOrWhiteSpace(compra.SecuencialModificado))
            {
                secMod = new string(compra.SecuencialModificado.Trim().Where(char.IsDigit).ToArray());
                if (secMod.Length == 0)
                    secMod = "000000001";
                else if (secMod.Length < 9)
                    secMod = secMod.PadLeft(9, '0');
                else if (secMod.Length > 9)
                    secMod = secMod.Substring(0, 9);
            }

            // autModificado - asegurar mínimo 37 dígitos
            string autMod = authDigits;
            if (autMod.Length < 37)
                autMod = autMod.PadLeft(37, '0');
            else if (autMod.Length > 49)
                autMod = autMod.Substring(0, 49);

            LogDocModificado($"BuildDocModificadoSimple (Compra): tipo={docMod}, estab={estabMod}, ptoEmi={ptoEmiMod}, sec={secMod}, auth='{autMod}'");

            // Generar campos simples como nodos independientes (formato ATS correcto)
            // No envolver en un elemento padre - retornar lista de XElement para agregar directamente
            return new List<XElement>
            {
                new XElement("docModificado", docMod),
                new XElement("estabModificado", estabMod),
                new XElement("ptoEmiModificado", ptoEmiMod),
                new XElement("secModificado", secMod),
                new XElement("autModificado", autMod)
            };
        }

        // Lightweight logger for docModificado generation
        private void LogDocModificado(string message)
        {
            try
            {
                string logDir = Path.Combine(AppContext.BaseDirectory, "Logs");
                Directory.CreateDirectory(logDir);
                string logPath = Path.Combine(logDir, "ATSXmlGenerator.log");
                File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch
            {
                // swallow logging errors to avoid impacting XML generation
            }
        }

        // Builds simple docModificado fields for NC (04) and ND (05) in Ventas - ATS schema expects flat elements
        internal List<XElement>? BuildDocModificadoSimpleVenta(Venta venta)
        {
            if (venta == null || (venta.TipoComprobante != "04" && venta.TipoComprobante != "05"))
            {
                return null;
            }

            string tipoComp = venta.TipoComprobante ?? "";
            if (tipoComp != "04" && tipoComp != "05")
            {
                return null;
            }

            // Obtener datos del documento modificado
            string authOriginal = venta.AutorizacionModificada ?? "";
            string authDigits = new string(authOriginal.Where(char.IsDigit).ToArray());

            // Si no tiene autorización válida (37+ dígitos), usar 37 nueves
            if (authDigits.Length < 37)
            {
                authDigits = new string('9', 37);
                LogDocModificado($"BuildDocModificadoSimpleVenta: Sin autorización válida, usando 37 nueves");
            }

            // docModificado - tipo de comprobante afectado (default 01 = factura)
            string docMod = "01";
            if (!string.IsNullOrWhiteSpace(venta.TipoComprobanteModificado) && venta.TipoComprobanteModificado.Length >= 2)
            {
                docMod = venta.TipoComprobanteModificado.Substring(0, 2);
            }

            // estabModificado - 3 digits
            string estabMod = "001";
            if (!string.IsNullOrWhiteSpace(venta.EstablecimientoModificado) && venta.EstablecimientoModificado.Length >= 3)
            {
                estabMod = venta.EstablecimientoModificado.Substring(0, 3);
            }

            // ptoEmiModificado - 3 digits
            string ptoEmiMod = "001";
            if (!string.IsNullOrWhiteSpace(venta.PuntoEmisionModificado) && venta.PuntoEmisionModificado.Length >= 3)
            {
                ptoEmiMod = venta.PuntoEmisionModificado.Substring(0, 3);
            }

            // secModificado - pad to 9 digits
            string secMod = "000000001";
            if (!string.IsNullOrWhiteSpace(venta.SecuencialModificado))
            {
                secMod = new string(venta.SecuencialModificado.Trim().Where(char.IsDigit).ToArray());
                if (secMod.Length == 0)
                    secMod = "000000001";
                else if (secMod.Length < 9)
                    secMod = secMod.PadLeft(9, '0');
                else if (secMod.Length > 9)
                    secMod = secMod.Substring(0, 9);
            }

            // autModificado - asegurar mínimo 37 dígitos
            string autMod = authDigits;
            if (autMod.Length < 37)
                autMod = autMod.PadLeft(37, '0');
            else if (autMod.Length > 49)
                autMod = autMod.Substring(0, 49);

            LogDocModificado($"BuildDocModificadoSimpleVenta: tipo={docMod}, estab={estabMod}, ptoEmi={ptoEmiMod}, sec={secMod}, auth='{autMod}'");

            // Generar campos simples como nodos independientes (formato ATS correcto)
            return new List<XElement>
            {
                new XElement("docModificado", docMod),
                new XElement("estabModificado", estabMod),
                new XElement("ptoEmiModificado", ptoEmiMod),
                new XElement("secModificado", secMod),
                new XElement("autModificado", autMod)
            };
        }

        private XElement GenerarDetallesVentas(IEnumerable<VentaAgrupada> ventas, CultureInfo xmlCulture)
        {
            XElement nodoVentas = new XElement("ventas");

            foreach (var venta in ventas)
            {
                decimal montoTotalVenta = venta.MontoTotal;

                string tpIdCliente = EscapeXml(SanitizeForXml(venta.TipoIdCliente));
                string idCliente = CleanNumericForXml(venta.IdCliente);
                string tipoComprobanteRaw = SanitizeForXml(venta.TipoComprobante);
                string tipoComprobante = tipoComprobanteRaw == "01" ? "18" : EscapeXml(tipoComprobanteRaw);
                string formaPago = CleanNumericForXml(venta.FormaPago ?? "01");

                bool incluirDenoCli = tpIdCliente != "04" && tpIdCliente != "05";

                var elementos = new List<XElement>
                {
                    new XElement("tpIdCliente", tpIdCliente),
                    new XElement("idCliente", idCliente),
                    new XElement("parteRelVtas", "NO")
                };

                if (incluirDenoCli)
                {
                    string denoCliLimpia = EscapeXml(CleanStringForXml(venta.RazonSocialCliente ?? string.Empty));
                    elementos.Add(new XElement("denoCli", denoCliLimpia));
                }

                elementos.Add(new XElement("tipoComprobante", tipoComprobante));
                elementos.Add(new XElement("tipoEmision", "F"));
                elementos.Add(new XElement("numeroComprobantes", venta.NumeroComprobantes.ToString()));
                elementos.Add(new XElement("baseNoGraIva", Math.Abs(venta.BaseNoGraIva).ToString("F2", xmlCulture)));
                elementos.Add(new XElement("baseImponible", Math.Abs(venta.BaseImponible).ToString("F2", xmlCulture)));
                elementos.Add(new XElement("baseImpGrav", Math.Abs(venta.BaseImpGrav).ToString("F2", xmlCulture)));
                elementos.Add(new XElement("montoIva", Math.Abs(venta.MontoIva).ToString("F2", xmlCulture)));
                elementos.Add(new XElement("montoIce", Math.Abs(venta.MontoIce).ToString("F2", xmlCulture)));
                elementos.Add(new XElement("valorRetIva", Math.Abs(venta.ValRetIva).ToString("F2", xmlCulture)));
                elementos.Add(new XElement("valorRetRenta", Math.Abs(venta.ValRetRenta).ToString("F2", xmlCulture)));

                elementos.Add(new XElement("formasDePago", new XElement("formaPago", formaPago)));

                XElement detalleVenta = new XElement("detalleVentas", elementos);
                nodoVentas.Add(detalleVenta);
            }
            return nodoVentas;
        }

        private XElement GenerarRetencionesRecibidas(IEnumerable<RetencionCliente> retenciones, CultureInfo xmlCulture)
        {
            var retencionesList = retenciones.ToList();
            if (!retencionesList.Any())
            {
                return new XElement("retenciones");
            }

            XElement nodoRetenciones = new XElement("retenciones");

            foreach (var ret in retencionesList)
            {
                string tpIdCliente = ret.RucEmisor?.Length == 13 ? "04" : ret.RucEmisor?.Length == 10 ? "05" : "06";
                
                var elementos = new List<XElement>
                {
                    new XElement("tpIdProveedor", EscapeXml(tpIdCliente)),
                    new XElement("idProveedor", CleanNumericForXml(ret.RucEmisor ?? "")),
                    new XElement("tipoComprobante", "07"),
                    new XElement("valorRetIva", Math.Abs(ret.ValRetIva ?? 0).ToString("F2", xmlCulture)),
                    new XElement("valorRetRenta", Math.Abs(ret.ValRetRenta ?? 0).ToString("F2", xmlCulture))
                };

                XElement detalleRetencion = new XElement("detalleRetencion", elementos);
                nodoRetenciones.Add(detalleRetencion);
            }
            return nodoRetenciones;
        }

        private XElement GenerarVentasEstablecimiento(IEnumerable<Venta> ventas, CultureInfo xmlCulture)
        {
            XElement nodoVentasEstab = new XElement("ventasEstablecimiento");

            // ventasEstab debe ser la suma de: baseImponible + baseImpGrav (sin IVA)
            decimal totalVentasConsolidado = ventas
                .Where(v => v.IdCliente != "9999999999999" || v.TipoComprobante == "04" || v.TipoComprobante == "05")
                .Sum(v => Math.Abs(v.BaseImponible ?? 0) + Math.Abs(v.BaseImpGrav ?? 0));

            XElement ventaEst = new XElement("ventaEst",
                new XElement("codEstab", "001"),
                new XElement("ventasEstab", totalVentasConsolidado.ToString("F2", xmlCulture)),
                new XElement("ivaComp", "0.00")
            );
            nodoVentasEstab.Add(ventaEst);
            return nodoVentasEstab;
        }

        private XElement GenerarRetencionesVentas(IEnumerable<RetencionCliente> retenciones, CultureInfo xmlCulture)
        {
            XElement nodoVentas = new XElement("ventas");

            foreach (var ret in retenciones)
            {
                string tpIdCliente = ret.RucEmisor?.Length == 13 ? "04" : ret.RucEmisor?.Length == 10 ? "05" : "06";
                
                var elementos = new List<XElement>
                {
                    new XElement("tpIdCliente", tpIdCliente),
                    new XElement("idCliente", CleanNumericForXml(ret.RucEmisor ?? "")),
                    new XElement("parteRelVtas", "NO")
                };

                if (tpIdCliente != "04" && tpIdCliente != "05")
                {
                    string denoLimpia = EscapeXml(CleanStringForXml(ret.RazonSocialEmisor ?? string.Empty));
                    elementos.Add(new XElement("denoCli", denoLimpia));
                }

                elementos.Add(new XElement("tipoComprobante", "07"));
                elementos.Add(new XElement("tipoEmision", "F"));
                elementos.Add(new XElement("numeroComprobantes", "1"));
                elementos.Add(new XElement("baseNoGraIva", "0.00"));
                elementos.Add(new XElement("baseImponible", Math.Abs(ret.BaseImpAir ?? 0).ToString("F2", xmlCulture)));
                elementos.Add(new XElement("baseImpGrav", Math.Abs(ret.BaseImpGrav ?? 0).ToString("F2", xmlCulture)));
                elementos.Add(new XElement("montoIva", Math.Abs(ret.ValRetIva ?? 0).ToString("F2", xmlCulture)));
                elementos.Add(new XElement("montoIce", "0.00"));
                elementos.Add(new XElement("valorRetIva", Math.Abs(ret.ValRetIva ?? 0).ToString("F2", xmlCulture)));
                elementos.Add(new XElement("valorRetRenta", Math.Abs(ret.ValRetRenta ?? 0).ToString("F2", xmlCulture)));

                XElement detalleVenta = new XElement("detalleVentas", elementos);
                nodoVentas.Add(detalleVenta);
            }
            return nodoVentas;
        }

        private XElement GenerarDetallesVentasConRetenciones(IEnumerable<Venta> ventas, IEnumerable<RetencionCliente> retenciones, CultureInfo xmlCulture)
        {
            XElement nodoVentas = new XElement("ventas");

            // Add regular ventas
            foreach (var venta in ventas)
            {
                decimal montoTotalVenta = venta.MontoTotal ?? 0.00M;
                bool esNotaCredito = venta.TipoComprobante == "04";

                string tpIdCliente = EscapeXml(SanitizeForXml(venta.TipoIdCliente ?? ""));
                string idCliente = CleanNumericForXml(venta.IdCliente ?? "");
                string tipoComprobanteRaw = SanitizeForXml(venta.TipoComprobante ?? "");
                string tipoComprobante = tipoComprobanteRaw == "01" ? "18" : EscapeXml(tipoComprobanteRaw);
                string formaPago = CleanNumericForXml(venta.FormaPago ?? "01");

                bool incluirDenoCli = tpIdCliente != "04" && tpIdCliente != "05";

                var elementos = new List<XElement>
                {
                    new XElement("tpIdCliente", tpIdCliente),
                    new XElement("idCliente", idCliente),
                    new XElement("parteRelVtas", "NO")
                };

                if (incluirDenoCli)
                {
                    string denoCliLimpia = EscapeXml(CleanStringForXml(venta.RazonSocialCliente ?? string.Empty));
                    elementos.Add(new XElement("denoCli", denoCliLimpia));
                }

                elementos.Add(new XElement("tipoComprobante", tipoComprobante));
                elementos.Add(new XElement("tipoEmision", "F"));
                elementos.Add(new XElement("numeroComprobantes", "1"));
                elementos.Add(new XElement("baseNoGraIva", Math.Abs(venta.BaseNoGraIva ?? 0.00M).ToString("F2", xmlCulture)));
                elementos.Add(new XElement("baseImponible", Math.Abs(venta.BaseImponible ?? 0.00M).ToString("F2", xmlCulture)));
                elementos.Add(new XElement("baseImpGrav", Math.Abs(venta.BaseImpGrav ?? 0.00M).ToString("F2", xmlCulture)));
                elementos.Add(new XElement("montoIva", Math.Abs(venta.MontoIva ?? 0.00M).ToString("F2", xmlCulture)));
                elementos.Add(new XElement("montoIce", Math.Abs(venta.MontoIce ?? 0.00M).ToString("F2", xmlCulture)));
                elementos.Add(new XElement("valorRetIva", Math.Abs(venta.valRetIVA ?? 0.00M).ToString("F2", xmlCulture)));
                elementos.Add(new XElement("valorRetRenta", Math.Abs(venta.valRetRenta ?? 0.00M).ToString("F2", xmlCulture)));

                elementos.Add(new XElement("formasDePago", new XElement("formaPago", formaPago)));

                if (venta.TipoComprobante == "04" || venta.TipoComprobante == "05")
                {
                    var camposModificados = BuildDocModificadoSimpleVenta(venta);
                    if (camposModificados != null)
                    {
                        foreach (var campo in camposModificados)
                        {
                            elementos.Add(campo);
                        }
                    }
                }

                XElement detalleVenta = new XElement("detalleVentas", elementos);
                nodoVentas.Add(detalleVenta);
            }

            // Add retenciones (tipoComprobante = 07)
            foreach (var ret in retenciones)
            {
                string tpIdCliente = ret.RucEmisor?.Length == 13 ? "04" : ret.RucEmisor?.Length == 10 ? "05" : "06";
                
                var elementos = new List<XElement>
                {
                    new XElement("tpIdCliente", tpIdCliente),
                    new XElement("idCliente", CleanNumericForXml(ret.RucEmisor ?? "")),
                    new XElement("parteRelVtas", "NO")
                };

                if (tpIdCliente != "04" && tpIdCliente != "05")
                {
                    string denoLimpia = EscapeXml(CleanStringForXml(ret.RazonSocialEmisor ?? string.Empty));
                    elementos.Add(new XElement("denoCli", denoLimpia));
                }

                elementos.Add(new XElement("tipoComprobante", "07"));
                elementos.Add(new XElement("tipoEmision", "F"));
                elementos.Add(new XElement("numeroComprobantes", "1"));
                elementos.Add(new XElement("baseNoGraIva", "0.00"));
                elementos.Add(new XElement("baseImponible", Math.Abs(ret.BaseImpAir ?? 0).ToString("F2", xmlCulture)));
                elementos.Add(new XElement("baseImpGrav", Math.Abs(ret.BaseImpGrav ?? 0).ToString("F2", xmlCulture)));
                elementos.Add(new XElement("montoIva", Math.Abs(ret.ValRetIva ?? 0).ToString("F2", xmlCulture)));
                elementos.Add(new XElement("montoIce", "0.00"));
                elementos.Add(new XElement("valorRetIva", Math.Abs(ret.ValRetIva ?? 0).ToString("F2", xmlCulture)));
                elementos.Add(new XElement("valorRetRenta", Math.Abs(ret.ValRetRenta ?? 0).ToString("F2", xmlCulture)));

                XElement detalleVenta = new XElement("detalleVentas", elementos);
                nodoVentas.Add(detalleVenta);
            }
            return nodoVentas;
        }
    }
}
