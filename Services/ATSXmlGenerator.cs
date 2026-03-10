using AtsManager.Models;
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

            string rucFinal = !string.IsNullOrWhiteSpace(rucOverride) ? rucOverride : _ruc;
            string razonSocialFinal = !string.IsNullOrWhiteSpace(razonSocialOverride)
                ? razonSocialOverride
                : _razonSocial;

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

            XElement raiz = new XElement("iva",

                new XElement("TipoIDInformante", rucFinal.Length == 13 ? "R" : "C"),
                new XElement("IdInformante", rucFinal),
                new XElement("razonSocial", razonSocialLimpia),
                new XElement("Anio", anio),
                new XElement("Mes", mes.ToString("D2")),

                new XElement("numEstabRuc", "001"),
                new XElement("totalVentas",
                    filteredVentas.Sum(v => v.MontoTotal).GetValueOrDefault().ToString("F2", xmlCulture)
                ),
                new XElement("codigoOperativo", "IVA"),

                GenerarDetallesCompras(filteredCompras, xmlCulture, mes, anio),
                GenerarDetallesVentas(filteredVentas, xmlCulture),
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

        private XElement GenerarDetallesCompras(IEnumerable<Compra> compras, CultureInfo xmlCulture, int mes, int anio)
        {
            XElement nodoCompras = new XElement("compras");

            foreach (var compra in compras)
            {
                decimal montoTotalCompra = compra.MontoTotal ?? 0.00M;
                bool requiereFormaPago = montoTotalCompra >= UMBRAL_PAGO_OBLIGATORIO;

                string numComprobante = compra.NumComprobante ?? string.Empty;
                var (establecimiento, puntoEmision, secuencial) = ExtraerComponentesComprobante(numComprobante);

                string denoProvLimpia = CleanStringForXml(compra.RazonSocialProveedor ?? string.Empty);

                DateTime fechaRegistro = compra.FechaRegistro.GetValueOrDefault();
                DateTime fechaEmision = compra.FechaEmision.GetValueOrDefault();

                if (fechaRegistro.Month != mes || fechaRegistro.Year != anio)
                {
                    fechaRegistro = fechaEmision;
                }

                XElement detalleAir = new XElement("air");

                if (compra.BaseImpAir.HasValue && compra.BaseImpAir.Value > 0)
                {
                    XElement subDetalleAir = new XElement("detalleAir",
                            new XElement("codRetAir", compra.CodRetAir),
                            new XElement("baseImpAir", (compra.BaseImpAir ?? 0.00M).ToString("F2", xmlCulture)),
                            new XElement("porcentajeAir", (compra.PorcentajeAir ?? 0.00M).ToString("F2", xmlCulture)),
                            new XElement("valRetAir", (compra.ValRetAir ?? 0.00M).ToString("F2", xmlCulture))
                        );
                    detalleAir.Add(subDetalleAir);
                }

                XElement detalleCompra = new XElement("detalleCompras",
                    new XElement("codSustento", compra.CodSustento),
                    new XElement("tpIdProv", compra.TipoIdProveedor),
                    new XElement("idProv", compra.IdProveedor),
                    new XElement("tipoComprobante", compra.TipoComprobante),
                    new XElement("tipoProv", compra.TipoProveedor),
                    new XElement("denoProv", denoProvLimpia),
                    new XElement("parteRel", compra.ParteRelacionada ? "SI" : "NO"),

                    new XElement("fechaRegistro", fechaRegistro.ToString("dd/MM/yyyy", xmlCulture)),
                    new XElement("establecimiento", establecimiento),
                    new XElement("puntoEmision", puntoEmision),
                    new XElement("secuencial", secuencial),
                    new XElement("fechaEmision", fechaEmision.ToString("dd/MM/yyyy", xmlCulture)),
                    new XElement("autorizacion", compra.Autorizacion),

                    new XElement("baseNoGraIva", Math.Abs(compra.BaseNoGraIva ?? 0.00M).ToString("F2", xmlCulture)),
                    new XElement("baseImponible", Math.Abs(compra.BaseImponible ?? 0.00M).ToString("F2", xmlCulture)),
                    new XElement("baseImpGrav", Math.Abs(compra.BaseImpGrav ?? 0.00M).ToString("F2", xmlCulture)),

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
                        new XElement("pagoLocExt", compra.PagoLocExt),
                        new XElement("paisEfecPago", "NA"),
                        new XElement("aplicConvDobTrib", "NA"),
                        new XElement("pagExtSujRetNorLeg", "NA")
                    ),

                    // Documento modificado (para NC/ND) - obligatorio si es NC o ND
                    (compra.TipoComprobante == "04" || compra.TipoComprobante == "05")
                    ? new XElement("docModificado",
                        new XElement("tipoComprobante", string.IsNullOrEmpty(compra.TipoComprobanteModificado) ? "01" : compra.TipoComprobanteModificado),
                        new XElement("establecimiento", string.IsNullOrEmpty(compra.EstablecimientoModificado) ? "001" : compra.EstablecimientoModificado),
                        new XElement("puntoEmision", string.IsNullOrEmpty(compra.PuntoEmisionModificado) ? "001" : compra.PuntoEmisionModificado),
                        new XElement("secuencial", string.IsNullOrEmpty(compra.SecuencialModificado) ? "000000001" : compra.SecuencialModificado),
                        new XElement("autorizacion", compra.AutorizacionModificada ?? ""))
                        : null,

                    requiereFormaPago ?
                        new XElement("formasDePago", new XElement("formaPago", compra.FormaPago ?? "01"))
                        : null,

                    detalleAir
                );
                nodoCompras.Add(detalleCompra);
            }
            return nodoCompras;
        }

        private XElement GenerarDetallesVentas(IEnumerable<Venta> ventas, CultureInfo xmlCulture)
        {
            XElement nodoVentas = new XElement("ventas");

            foreach (var venta in ventas)
            {
                decimal montoTotalVenta = venta.MontoTotal ?? 0.00M;
                bool requiereFormaPago = montoTotalVenta >= UMBRAL_PAGO_OBLIGATORIO;

                if (venta.IdCliente == "9999999999999") continue;

                string denoCliLimpia = CleanStringForXml(venta.RazonSocialCliente ?? string.Empty);

                XElement detalleVenta = new XElement("detalleVentas",
                    new XElement("tpIdCliente", venta.TipoIdCliente ?? ""),
                    new XElement("idCliente", venta.IdCliente ?? ""),
                    new XElement("denoCli", denoCliLimpia),
                    new XElement("parteRelVtas", "NO"),
                    new XElement("tipoComprobante", venta.TipoComprobante ?? ""),

                    new XElement("tipoEmision", "E"),
                    new XElement("numeroComprobantes", "1"),

                    new XElement("baseNoGraIva", Math.Abs(venta.BaseNoGraIva ?? 0.00M).ToString("F2", xmlCulture)),
                    new XElement("baseImponible", Math.Abs(venta.BaseImponible ?? 0.00M).ToString("F2", xmlCulture)),
                    new XElement("baseImpGrav", Math.Abs(venta.BaseImpGrav ?? 0.00M).ToString("F2", xmlCulture)),
                    new XElement("montoIva", Math.Abs(venta.MontoIva ?? 0.00M).ToString("F2", xmlCulture)),
                    new XElement("montoIce", Math.Abs(venta.MontoIce ?? 0.00M).ToString("F2", xmlCulture)),

                    new XElement("valRetIVA", Math.Abs(venta.valRetIVA ?? 0.00M).ToString("F2", xmlCulture)),
                    new XElement("valRetRenta", Math.Abs(venta.valRetRenta ?? 0.00M).ToString("F2", xmlCulture)),

                    new XElement("codEstab", "001"),
                    new XElement("ventasEstab", Math.Abs(venta.MontoTotal ?? 0.00M).ToString("F2", xmlCulture)),
                    new XElement("ivaComp", "0.00"),

                    requiereFormaPago ?
                        new XElement("formasDePago", new XElement("formaPago", venta.FormaPago ?? "01"))
                        : null
                );
                nodoVentas.Add(detalleVenta);
            }
            return nodoVentas;
        }

        private XElement GenerarVentasEstablecimiento(IEnumerable<Venta> ventas, CultureInfo xmlCulture)
        {
            XElement nodoVentasEstab = new XElement("ventasEstablecimiento");

            decimal totalVentasConsolidado = ventas.Sum(v => v.MontoTotal).GetValueOrDefault();

            XElement ventaEst = new XElement("ventaEst",
                new XElement("codEstab", "001"),
                new XElement("ventasEstab", totalVentasConsolidado.ToString("F2", xmlCulture)),
                new XElement("ivaComp", "0.00")
            );
            nodoVentasEstab.Add(ventaEst);
            return nodoVentasEstab;
        }
    }
}
