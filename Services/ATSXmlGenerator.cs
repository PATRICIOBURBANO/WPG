using AtsManager.Models;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using AtsManager.Services;

namespace AtsManager.ServicesA
{
    public class ATSXmlGenerator
    {
        private readonly string _ruc;
        private readonly string _razonSocial;

        public string Ruc => _ruc;

        public ATSXmlGenerator(string ruc, string razonSocial)
        {
            _ruc = ruc;
            _razonSocial = razonSocial;
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

            // Se elimina el guion ('-') solo de la limpieza general de textos, no aplica a campos numéricos
            cleaned = cleaned.Replace(".", "").Replace(",", "").Replace("  ", " ").Trim();

            // Si se necesita eliminar guiones para campos de texto, se puede añadir aquí
            // cleaned = cleaned.Replace("-", " "); // Descomentar si es necesario

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

        public byte[] GenerarXmlBytes(int mes, int anio, List<Compra> compras, List<Venta> ventas)
        {
            var xmlCulture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            xmlCulture.NumberFormat.NumberDecimalSeparator = ".";

            string razonSocialLimpia = CleanStringForXml(_razonSocial);

            XElement raiz = new XElement(XName.Get("iva"),

                // --- HEADER ---
                new XElement("TipoIDInformante", _ruc.Length == 13 ? "R" : "C"),
                new XElement("IdInformante", _ruc),
                new XElement("razonSocial", razonSocialLimpia),
                new XElement("Anio", anio),
                new XElement("Mes", mes.ToString("D2")),

                new XElement("numEstabRuc", "001"),
                new XElement("totalVentas", ventas.Sum(v => v.MontoTotal).GetValueOrDefault().ToString("F2", xmlCulture)),
                new XElement("codigoOperativo", "IVA"),

                GenerarDetallesCompras(compras, xmlCulture),
                GenerarDetallesVentas(ventas, xmlCulture),
                GenerarVentasEstablecimiento(xmlCulture)
            );

            // Declaración XML
            XDocument document = new XDocument(
                new XDeclaration("1.0", "UTF-8", "yes"),
                raiz
            );

            // 3. CONVERSIÓN FINAL A BYTE ARRAY
            using (var memoryStream = new MemoryStream())
            {
                using (var writer = new StreamWriter(memoryStream, new UTF8Encoding(false)))
                {
                    document.Save(writer, SaveOptions.DisableFormatting);
                }
                return memoryStream.ToArray();
            }
        }

        private XElement GenerarDetallesCompras(List<Compra> compras, CultureInfo xmlCulture)
        {
            XElement nodoCompras = new XElement("compras");
            XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";

            foreach (var compra in compras)
            {
                // 🎯 INICIO DE LA LÓGICA DE RESPALDO (FALLBACK) PARA EVITAR ERRORES DEL DIMM 🎯

                string establecimiento;
                string puntoEmision;
                string secuencial;

                if (!string.IsNullOrWhiteSpace(compra.Estab) && !string.IsNullOrWhiteSpace(compra.PtoEmi) && !string.IsNullOrWhiteSpace(compra.Secuencial))
                {
                    // Opción A: Usar los campos limpios del modelo Compra (Corregido para XML)
                    establecimiento = compra.Estab;
                    puntoEmision = compra.PtoEmi;
                    secuencial = compra.Secuencial;
                }
                else
                {
                    // Opción B: Usar la lógica antigua de Substring (Mantiene compatibilidad con TXT)
                    // NOTA: Esta lógica asume que NumComprobante no tiene guiones, o que su lógica anterior los manejaba bien.
                    establecimiento = compra.NumComprobante.Length >= 3 ? compra.NumComprobante.Substring(0, 3) : "001";
                    puntoEmision = compra.NumComprobante.Length >= 6 ? compra.NumComprobante.Substring(3, 3) : "001";
                    secuencial = compra.NumComprobante.Length > 6 ? compra.NumComprobante.Substring(6) : "000000001";
                }

                // 🎯 FIN DE LA LÓGICA DE RESPALDO 🎯

                string denoProvLimpia = CleanStringForXml(compra.RazonSocialProveedor);

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

                    new XElement("fechaRegistro", compra.FechaRegistro.GetValueOrDefault().ToString("dd/MM/yyyy", xmlCulture)),
                    new XElement("establecimiento", establecimiento), // <<-- USA LA VARIABLE CORREGIDA
                    new XElement("puntoEmision", puntoEmision),       // <<-- USA LA VARIABLE CORREGIDA
                    new XElement("secuencial", secuencial),           // <<-- USA LA VARIABLE CORREGIDA
                    new XElement("fechaEmision", compra.FechaEmision.GetValueOrDefault().ToString("dd/MM/yyyy", xmlCulture)),
                    new XElement("autorizacion", compra.Autorizacion),

                    new XElement("baseNoGraIva", (compra.BaseImponible ).ToString("F2", xmlCulture)),
                    new XElement("baseImponible", (compra.BaseNoGraIva ?? 0.00M).ToString("F2", xmlCulture)),
                    new XElement("baseImpGrav", (compra.BaseImpGrav ?? 0.00M).ToString("F2", xmlCulture)),
                    new XElement("baseImpExe", (compra.BaseImpExe ?? 0.00M).ToString("F2", xmlCulture)),
                    new XElement("montoIce", (compra.MontoIce ?? 0.00M).ToString("F2", xmlCulture)),
                    new XElement("montoIva", (compra.MontoIva ?? 0.00M).ToString("F2", xmlCulture)),

                    new XElement("valRetBien10", (compra.ValRetBien10 ?? 0.00M).ToString("F2", xmlCulture)),
                    new XElement("valRetServ20", (compra.ValRetServ20 ?? 0.00M).ToString("F2", xmlCulture)),
                    new XElement("valorRetBienes", (compra.ValorRetBienes ?? 0.00M).ToString("F2", xmlCulture)),
                    new XElement("valRetServ50", (compra.ValRetServ50 ?? 0.00M).ToString("F2", xmlCulture)),
                    new XElement("valorRetServicios", (compra.ValorRetServicios ?? 0.00M).ToString("F2", xmlCulture)),
                    new XElement("valRetServ100", (compra.ValRetServ100 ?? 0.00M).ToString("F2", xmlCulture)),
                    new XElement("valorRetencionNc", (compra.ValorRetencionNc ?? 0.00M).ToString("F2", xmlCulture)),

                    new XElement("totbasesImpReemb", "0.00"),
                    new XElement("pagoExterior",
                        new XElement("pagoLocExt", compra.PagoLocExt),
                        new XElement("paisEfecPago", "NA"),
                        new XElement("aplicConvDobTrib", "NA"),
                        new XElement("pagExtSujRetNorLeg", "NA")
                    ),
                    new XElement("formasDePago",
                        new XElement("formaPago", compra.FormaPago)
                    ),

                    detalleAir
                );
                nodoCompras.Add(detalleCompra);
            }
            return nodoCompras;
        }

        private XElement GenerarDetallesVentas(List<Venta> ventas, CultureInfo xmlCulture)
        {
            XElement nodoVentas = new XElement("ventas");

            foreach (var venta in ventas)
            {
                if (venta.IdCliente == "9999999999999") continue;

                string denoCliLimpia = CleanStringForXml(venta.RazonSocialCliente);

                XElement detalleVenta = new XElement("detalleVentas",
                    new XElement("tpIdCliente", venta.TipoIdCliente),
                    new XElement("idCliente", venta.IdCliente),
                    new XElement("denoCli", denoCliLimpia),
                    new XElement("parteRelVtas", "NO"),
                    new XElement("tipoComprobante", venta.TipoComprobante),

                    new XElement("tipoEmision", "E"),
                    new XElement("numeroComprobantes", "1"),

                    new XElement("baseNoGraIva", (venta.BaseNoGraIva ?? 0.00M).ToString("F2", xmlCulture)),
                    new XElement("baseImponible", (venta.BaseImpGrav ?? 0.00M).ToString("F2", xmlCulture)),
                    new XElement("baseImpGrav", (venta.BaseImpGrav ?? 0.00M).ToString("F2", xmlCulture)),
                    new XElement("montoIva", (venta.MontoIva ?? 0.00M).ToString("F2", xmlCulture)),
                    new XElement("montoIce", (venta.MontoIce ?? 0.00M).ToString("F2", xmlCulture)),

                    new XElement("valRetIVA", (venta.valRetIVA ?? 0.00M).ToString("F2", xmlCulture)),
                    new XElement("valRetRenta", (venta.valRetRenta ?? 0.00M).ToString("F2", xmlCulture)),

                    new XElement("codEstab", "001"),
                    new XElement("ventasEstab", (venta.MontoTotal ?? 0.00M).ToString("F2", xmlCulture)),
                    new XElement("ivaComp", "0.00"),

                    new XElement("formasDePago",
                    new XElement("formaPago", "20")
                    )
                );
                nodoVentas.Add(detalleVenta);
            }
            return nodoVentas;
        }

        private XElement GenerarVentasEstablecimiento(CultureInfo xmlCulture)
        {
            XElement nodoVentasEstab = new XElement("ventasEstablecimiento");
            XElement ventaEst = new XElement("ventaEst",
                new XElement("codEstab", "001"),
                new XElement("ventasEstab", "0.00"),
                new XElement("ivaComp", "0.00")
            );
            nodoVentasEstab.Add(ventaEst);
            return nodoVentasEstab;
        }
    }
}