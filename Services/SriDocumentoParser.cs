using AtsManager.Pages.Empresas.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace AtsManager.Services
{
    public interface ISriDocumentParser
    {
        (SriDocumento documento, List<string> errores) ParsearDocumento(string xmlPath, string rucEmpresa, DireccionDocumento direccion, short periodoAnio, short periodoMes);
        (SriDocumento documento, List<string> errores) ParsearDocumentoDesdeXml(string xmlContenido, string rucEmpresa, DireccionDocumento direccion, short periodoAnio, short periodoMes);
    }

    public class SriDocumentoParser : ISriDocumentParser
    {
        private readonly AtsDbContext _context;

        public SriDocumentoParser(AtsDbContext context)
        {
            _context = context;
        }

        public (SriDocumento documento, List<string> errores) ParsearDocumento(
            string xmlPath, string rucEmpresa, DireccionDocumento direccion, short periodoAnio, short periodoMes)
        {
            var errores = new List<string>();

            try
            {
                if (!File.Exists(xmlPath))
                {
                    errores.Add($"Archivo no encontrado: {xmlPath}");
                    return (null!, errores);
                }

                var xmlContenido = File.ReadAllText(xmlPath);
                return ParsearDocumentoDesdeXml(xmlContenido, rucEmpresa, direccion, periodoAnio, periodoMes);
            }
            catch (Exception ex)
            {
                errores.Add($"Error al leer archivo: {ex.Message}");
                return (null!, errores);
            }
        }

        public (SriDocumento documento, List<string> errores) ParsearDocumentoDesdeXml(
            string xmlContenido, string rucEmpresa, DireccionDocumento direccion, short periodoAnio, short periodoMes)
        {
            var errores = new List<string>();

            try
            {
                XDocument xmlDoc = XDocument.Parse(xmlContenido);
                XElement? root = xmlDoc.Root;

                if (root != null && root.Name.LocalName.Equals("autorizacion", StringComparison.OrdinalIgnoreCase))
                {
                    var comprobante = root.Descendants()
                        .FirstOrDefault(e => e.Name.LocalName.Equals("comprobante", StringComparison.OrdinalIgnoreCase))?.Value;

                    if (!string.IsNullOrWhiteSpace(comprobante))
                    {
                        xmlDoc = XDocument.Parse(comprobante);
                    }
                }

                if (xmlDoc.Root == null)
                {
                    errores.Add("XML inválido: no se encontró elemento raíz");
                    return (null!, errores);
                }

                string rootName = xmlDoc.Root.Name.LocalName.ToLowerInvariant();
                if (rootName == "comprobanteretencion") rootName = "comprobanteRetencion";

                return rootName switch
                {
                    "factura" => ParsearFactura(xmlDoc, rucEmpresa, direccion, periodoAnio, periodoMes, errores),
                    "notacredito" => ParsearNotaCredito(xmlDoc, rucEmpresa, direccion, periodoAnio, periodoMes, errores),
                    "notadebito" => ParsearNotaDebito(xmlDoc, rucEmpresa, direccion, periodoAnio, periodoMes, errores),
                    "comprobanteRetencion" => ParsearRetencion(xmlDoc, rucEmpresa, direccion, periodoAnio, periodoMes, errores),
                    _ => (null!, errores.Append($"Tipo de documento no reconocido: {rootName}").ToList())
                };
            }
            catch (Exception ex)
            {
                errores.Add($"Error al parsear XML: {ex.Message}");
                return (null!, errores);
            }
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

            var infoNodes = new[] { "infoFactura", "infoNotaCredito", "infoNotaDebito", "infoCompRetencion" };

            foreach (var nodeName in infoNodes)
            {
                var infoNode = GetFirstDescendant(xmlDoc, nodeName);
                if (infoNode != null)
                {
                    var fechaStr = GetChildValue(infoNode, "fechaEmision");
                    if (!string.IsNullOrEmpty(fechaStr) &&
                        DateTime.TryParseExact(fechaStr, dateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime fecha))
                    {
                        return fecha;
                    }
                }
            }
            return null;
        }

        private (SriDocumento doc, List<string> errores) ParsearFactura(
            XDocument xmlDoc, string rucEmpresa, DireccionDocumento direccion, short periodoAnio, short periodoMes, List<string> errores)
        {
            var infoTributaria = GetFirstDescendant(xmlDoc, "infoTributaria");
            var infoFactura = GetFirstDescendant(xmlDoc, "infoFactura");

            if (infoTributaria == null || infoFactura == null)
            {
                errores.Add("Estructura de factura inválida");
                return (null!, errores);
            }

            var doc = new SriDocumento
            {
                EmpresaId = ObtenerEmpresaId(rucEmpresa),
                PeriodoAnio = periodoAnio,
                PeriodoMes = periodoMes,
                ModuloAts = direccion == DireccionDocumento.RECIBIDO ? ModuloAts.COMPRA : ModuloAts.VENTA,
                DireccionDocumento = direccion,
                TipoComprobanteCodigo = GetChildValue(infoTributaria, "codDoc") ?? "01",
                EsElectronico = true,
                EstadoDocumento = EstadoDocumento.PROCESADO,
                ClaveAcceso = GetChildValue(infoTributaria, "claveAcceso"),
                NumeroAutorizacion = GetChildValue(infoTributaria, "claveAcceso"),
                Establecimiento = NormalizarCodigo(GetChildValue(infoTributaria, "estab") ?? "001", 3),
                PuntoEmision = NormalizarCodigo(GetChildValue(infoTributaria, "ptoEmi") ?? "001", 3),
                Secuencial = NormalizarSecuencial(GetChildValue(infoTributaria, "secuencial") ?? "000000001"),
                NumeroDocumento = $"{NormalizarCodigo(GetChildValue(infoTributaria, "estab") ?? "001", 3)}-{NormalizarCodigo(GetChildValue(infoTributaria, "ptoEmi") ?? "001", 3)}-{NormalizarSecuencial(GetChildValue(infoTributaria, "secuencial") ?? "000000001")}",
                FechaEmision = ObtenerFechaEmision(xmlDoc),
                FechaAutorizacion = ObtenerFechaEmision(xmlDoc),
                RucEmisor = GetChildValue(infoTributaria, "ruc"),
                RazonSocialEmisor = GetChildValue(infoTributaria, "razonSocial"),
                IdentificacionContraparte = GetChildValue(infoFactura, "identificacionComprador") ?? "",
                TipoIdentificacionContraparte = ObtenerTipoIdentificacion(GetChildValue(infoFactura, "identificacionComprador") ?? ""),
                RazonSocialContraparte = GetChildValue(infoFactura, "razonSocialComprador") ?? "",
                ParteRelacionada = false,
                CodSustento = "01",
                PagoLocExt = "01"
            };

            decimal.TryParse(GetChildValue(infoFactura, "totalSinImpuestos") ?? "0", NumberStyles.Any, CultureInfo.InvariantCulture, out decimal baseImp);
            decimal.TryParse(GetChildValue(infoFactura, "importeTotal") ?? "0", NumberStyles.Any, CultureInfo.InvariantCulture, out decimal montoTotal);

            decimal montoIva = xmlDoc.Descendants()
                .Where(e => e.Name.LocalName.Equals("totalImpuesto", StringComparison.OrdinalIgnoreCase) &&
                           GetChildValue(e, "codigo") == "2")
                .Sum(ti => decimal.TryParse(GetChildValue(ti, "valor") ?? "0", NumberStyles.Any, CultureInfo.InvariantCulture, out decimal v) ? v : 0);

            decimal montoIce = xmlDoc.Descendants()
                .Where(e => e.Name.LocalName.Equals("totalImpuesto", StringComparison.OrdinalIgnoreCase) &&
                           GetChildValue(e, "codigo") == "3")
                .Sum(ti => decimal.TryParse(GetChildValue(ti, "valor") ?? "0", NumberStyles.Any, CultureInfo.InvariantCulture, out decimal v) ? v : 0);

            if (montoIva > 0)
            {
                doc.BaseImpGrav = Math.Round(montoIva / 0.15m, 2);
                doc.BaseNoGraIva = Math.Round(baseImp - doc.BaseImpGrav.Value, 2);
                if (doc.BaseNoGraIva < 0) doc.BaseNoGraIva = 0;
            }
            else
            {
                doc.BaseNoGraIva = baseImp;
            }

            doc.MontoIva = montoIva;
            doc.MontoIce = montoIce;
            doc.TotalSinImpuestos = baseImp;
            doc.ImporteTotal = montoTotal;

            var formaPago = GetChildValue(infoFactura, "formaPago");
            if (!string.IsNullOrEmpty(formaPago))
            {
                doc.FormasPago.Add(new SriDocumentoFormaPago
                {
                    FormaPagoCodigo = formaPago,
                    MontoAsignado = montoTotal,
                    Orden = 1
                });
            }

            return (doc, errores);
        }

        private (SriDocumento doc, List<string> errores) ParsearNotaCredito(
            XDocument xmlDoc, string rucEmpresa, DireccionDocumento direccion, short periodoAnio, short periodoMes, List<string> errores)
        {
            var infoTributaria = GetFirstDescendant(xmlDoc, "infoTributaria");
            var infoNotaCredito = GetFirstDescendant(xmlDoc, "infoNotaCredito");

            if (infoTributaria == null || infoNotaCredito == null)
            {
                errores.Add("Estructura de nota de crédito inválida");
                return (null!, errores);
            }

            var doc = new SriDocumento
            {
                EmpresaId = ObtenerEmpresaId(rucEmpresa),
                PeriodoAnio = periodoAnio,
                PeriodoMes = periodoMes,
                ModuloAts = direccion == DireccionDocumento.RECIBIDO ? ModuloAts.COMPRA : ModuloAts.VENTA,
                DireccionDocumento = direccion,
                TipoComprobanteCodigo = "04",
                EsElectronico = true,
                EstadoDocumento = EstadoDocumento.PROCESADO,
                ClaveAcceso = GetChildValue(infoTributaria, "claveAcceso"),
                NumeroAutorizacion = GetChildValue(infoTributaria, "claveAcceso"),
                Establecimiento = NormalizarCodigo(GetChildValue(infoTributaria, "estab") ?? "001", 3),
                PuntoEmision = NormalizarCodigo(GetChildValue(infoTributaria, "ptoEmi") ?? "001", 3),
                Secuencial = NormalizarSecuencial(GetChildValue(infoTributaria, "secuencial") ?? "000000001"),
                NumeroDocumento = $"{NormalizarCodigo(GetChildValue(infoTributaria, "estab") ?? "001", 3)}-{NormalizarCodigo(GetChildValue(infoTributaria, "ptoEmi") ?? "001", 3)}-{NormalizarSecuencial(GetChildValue(infoTributaria, "secuencial") ?? "000000001")}",
                FechaEmision = ObtenerFechaEmision(xmlDoc),
                FechaAutorizacion = ObtenerFechaEmision(xmlDoc),
                RucEmisor = GetChildValue(infoTributaria, "ruc"),
                RazonSocialEmisor = GetChildValue(infoTributaria, "razonSocial"),
                IdentificacionContraparte = GetChildValue(infoNotaCredito, "identificacionComprador") ?? "",
                TipoIdentificacionContraparte = ObtenerTipoIdentificacion(GetChildValue(infoNotaCredito, "identificacionComprador") ?? ""),
                RazonSocialContraparte = GetChildValue(infoNotaCredito, "razonSocialComprador") ?? "",
                ParteRelacionada = false,
                CodSustento = "01",
                PagoLocExt = "01"
            };

            decimal.TryParse(GetChildValue(infoNotaCredito, "totalSinImpuestos") ?? "0", NumberStyles.Any, CultureInfo.InvariantCulture, out decimal baseImp);
            decimal.TryParse(GetChildValue(infoNotaCredito, "importeTotal") ?? "0", NumberStyles.Any, CultureInfo.InvariantCulture, out decimal montoTotal);

            decimal montoIva = xmlDoc.Descendants()
                .Where(e => e.Name.LocalName.Equals("totalImpuesto", StringComparison.OrdinalIgnoreCase) &&
                           GetChildValue(e, "codigo") == "2")
                .Sum(ti => decimal.TryParse(GetChildValue(ti, "valor") ?? "0", NumberStyles.Any, CultureInfo.InvariantCulture, out decimal v) ? v : 0);

            decimal montoIce = xmlDoc.Descendants()
                .Where(e => e.Name.LocalName.Equals("totalImpuesto", StringComparison.OrdinalIgnoreCase) &&
                           GetChildValue(e, "codigo") == "3")
                .Sum(ti => decimal.TryParse(GetChildValue(ti, "valor") ?? "0", NumberStyles.Any, CultureInfo.InvariantCulture, out decimal v) ? v : 0);

            if (montoIva > 0)
            {
                doc.BaseImpGrav = Math.Round(montoIva / 0.15m, 2);
                doc.BaseNoGraIva = Math.Round(baseImp - doc.BaseImpGrav.Value, 2);
                if (doc.BaseNoGraIva < 0) doc.BaseNoGraIva = 0;
            }
            else
            {
                doc.BaseNoGraIva = baseImp;
            }

            doc.MontoIva = montoIva;
            doc.MontoIce = montoIce;
            doc.TotalSinImpuestos = baseImp;
            doc.ImporteTotal = montoTotal;

            string numDocModificado = GetChildValue(infoNotaCredito, "numDocModificado") ?? "";
            string codDocModificado = GetChildValue(infoNotaCredito, "codDocModificado") ?? "";

            doc.DocumentoModificado = ParsearDocumentoModificado(numDocModificado, codDocModificado);

            return (doc, errores);
        }

        private (SriDocumento doc, List<string> errores) ParsearNotaDebito(
            XDocument xmlDoc, string rucEmpresa, DireccionDocumento direccion, short periodoAnio, short periodoMes, List<string> errores)
        {
            var infoTributaria = GetFirstDescendant(xmlDoc, "infoTributaria");
            var infoNotaDebito = GetFirstDescendant(xmlDoc, "infoNotaDebito");

            if (infoTributaria == null || infoNotaDebito == null)
            {
                errores.Add("Estructura de nota de débito inválida");
                return (null!, errores);
            }

            var doc = new SriDocumento
            {
                EmpresaId = ObtenerEmpresaId(rucEmpresa),
                PeriodoAnio = periodoAnio,
                PeriodoMes = periodoMes,
                ModuloAts = direccion == DireccionDocumento.RECIBIDO ? ModuloAts.COMPRA : ModuloAts.VENTA,
                DireccionDocumento = direccion,
                TipoComprobanteCodigo = "03",
                EsElectronico = true,
                EstadoDocumento = EstadoDocumento.PROCESADO,
                ClaveAcceso = GetChildValue(infoTributaria, "claveAcceso"),
                NumeroAutorizacion = GetChildValue(infoTributaria, "claveAcceso"),
                Establecimiento = NormalizarCodigo(GetChildValue(infoTributaria, "estab") ?? "001", 3),
                PuntoEmision = NormalizarCodigo(GetChildValue(infoTributaria, "ptoEmi") ?? "001", 3),
                Secuencial = NormalizarSecuencial(GetChildValue(infoTributaria, "secuencial") ?? "000000001"),
                NumeroDocumento = $"{NormalizarCodigo(GetChildValue(infoTributaria, "estab") ?? "001", 3)}-{NormalizarCodigo(GetChildValue(infoTributaria, "ptoEmi") ?? "001", 3)}-{NormalizarSecuencial(GetChildValue(infoTributaria, "secuencial") ?? "000000001")}",
                FechaEmision = ObtenerFechaEmision(xmlDoc),
                FechaAutorizacion = ObtenerFechaEmision(xmlDoc),
                RucEmisor = GetChildValue(infoTributaria, "ruc"),
                RazonSocialEmisor = GetChildValue(infoTributaria, "razonSocial"),
                IdentificacionContraparte = GetChildValue(infoNotaDebito, "identificacionComprador") ?? "",
                TipoIdentificacionContraparte = ObtenerTipoIdentificacion(GetChildValue(infoNotaDebito, "identificacionComprador") ?? ""),
                RazonSocialContraparte = GetChildValue(infoNotaDebito, "razonSocialComprador") ?? "",
                ParteRelacionada = false,
                CodSustento = "01",
                PagoLocExt = "01"
            };

            decimal.TryParse(GetChildValue(infoNotaDebito, "totalSinImpuestos") ?? "0", NumberStyles.Any, CultureInfo.InvariantCulture, out decimal baseImp);
            decimal.TryParse(GetChildValue(infoNotaDebito, "importeTotal") ?? "0", NumberStyles.Any, CultureInfo.InvariantCulture, out decimal montoTotal);

            decimal montoIva = xmlDoc.Descendants()
                .Where(e => e.Name.LocalName.Equals("totalImpuesto", StringComparison.OrdinalIgnoreCase) &&
                           GetChildValue(e, "codigo") == "2")
                .Sum(ti => decimal.TryParse(GetChildValue(ti, "valor") ?? "0", NumberStyles.Any, CultureInfo.InvariantCulture, out decimal v) ? v : 0);

            if (montoIva > 0)
            {
                doc.BaseImpGrav = Math.Round(montoIva / 0.15m, 2);
                doc.BaseNoGraIva = Math.Round(baseImp - doc.BaseImpGrav.Value, 2);
                if (doc.BaseNoGraIva < 0) doc.BaseNoGraIva = 0;
            }
            else
            {
                doc.BaseNoGraIva = baseImp;
            }

            doc.MontoIva = montoIva;
            doc.TotalSinImpuestos = baseImp;
            doc.ImporteTotal = montoTotal;

            string numDocModificado = GetChildValue(infoNotaDebito, "numDocModificado") ?? "";
            string codDocModificado = GetChildValue(infoNotaDebito, "codDocModificado") ?? "";

            doc.DocumentoModificado = ParsearDocumentoModificado(numDocModificado, codDocModificado);

            return (doc, errores);
        }

        private (SriDocumento doc, List<string> errores) ParsearRetencion(
            XDocument xmlDoc, string rucEmpresa, DireccionDocumento direccion, short periodoAnio, short periodoMes, List<string> errores)
        {
            var infoTributaria = GetFirstDescendant(xmlDoc, "infoTributaria");
            var infoRetencion = GetFirstDescendant(xmlDoc, "infoCompRetencion");

            if (infoTributaria == null || infoRetencion == null)
            {
                errores.Add("Estructura de retención inválida");
                return (null!, errores);
            }

            var doc = new SriDocumento
            {
                EmpresaId = ObtenerEmpresaId(rucEmpresa),
                PeriodoAnio = periodoAnio,
                PeriodoMes = periodoMes,
                ModuloAts = ModuloAts.COMPRA,
                DireccionDocumento = DireccionDocumento.RECIBIDO,
                TipoComprobanteCodigo = "07",
                EsElectronico = true,
                EstadoDocumento = EstadoDocumento.PROCESADO,
                ClaveAcceso = GetChildValue(infoTributaria, "claveAcceso"),
                NumeroAutorizacion = GetChildValue(infoTributaria, "claveAcceso"),
                Establecimiento = NormalizarCodigo(GetChildValue(infoTributaria, "estab") ?? "001", 3),
                PuntoEmision = NormalizarCodigo(GetChildValue(infoTributaria, "ptoEmi") ?? "001", 3),
                Secuencial = NormalizarSecuencial(GetChildValue(infoTributaria, "secuencial") ?? "000000001"),
                NumeroDocumento = $"{NormalizarCodigo(GetChildValue(infoTributaria, "estab") ?? "001", 3)}-{NormalizarCodigo(GetChildValue(infoTributaria, "ptoEmi") ?? "001", 3)}-{NormalizarSecuencial(GetChildValue(infoTributaria, "secuencial") ?? "000000001")}",
                FechaEmision = ObtenerFechaEmision(xmlDoc),
                FechaAutorizacion = ObtenerFechaEmision(xmlDoc),
                RucEmisor = GetChildValue(infoTributaria, "ruc"),
                RazonSocialEmisor = GetChildValue(infoTributaria, "razonSocial"),
                IdentificacionContraparte = GetChildValue(infoRetencion, "identificacionSujetoRetenido") ?? "",
                TipoIdentificacionContraparte = "01",
                RazonSocialContraparte = GetChildValue(infoRetencion, "razonSocialSujetoRetenido") ?? "",
                ParteRelacionada = false,
                CodSustento = "01",
                PagoLocExt = "01"
            };

            var impuestos = xmlDoc.Descendants()
                .Where(e => e.Name.LocalName.Equals("impuesto", StringComparison.OrdinalIgnoreCase));

            foreach (var imp in impuestos)
            {
                string codigo = GetChildValue(imp, "codigo") ?? "";
                decimal.TryParse(GetChildValue(imp, "baseImponible") ?? "0", NumberStyles.Any, CultureInfo.InvariantCulture, out decimal baseImp);
                decimal.TryParse(GetChildValue(imp, "porcentaje") ?? "0", NumberStyles.Any, CultureInfo.InvariantCulture, out decimal porcentaje);
                decimal.TryParse(GetChildValue(imp, "valorRetenido") ?? "0", NumberStyles.Any, CultureInfo.InvariantCulture, out decimal valorRet);

                if (codigo == "1")
                {
                    doc.RetencionesRenta.Add(new SriDocumentoRetencionRenta
                    {
                        CodRetAir = GetChildValue(imp, "codigoRetencion") ?? "332",
                        BaseImpAir = baseImp,
                        PorcentajeAir = porcentaje,
                        ValRetAir = valorRet
                    });
                }
                else if (codigo == "2")
                {
                    string tarifa = GetChildValue(imp, "tarifa") ?? "";
                    if (tarifa == "10" || porcentaje == 10)
                        doc.RetencionesIva.Add(new SriDocumentoRetencionIva { ValRetBien10 = valorRet });
                    else if (tarifa == "20" || porcentaje == 20)
                        doc.RetencionesIva.Add(new SriDocumentoRetencionIva { ValRetServ20 = valorRet });
                    else if (tarifa == "30" || porcentaje == 30)
                        doc.RetencionesIva.Add(new SriDocumentoRetencionIva { ValorRetBienes30 = valorRet });
                    else if (tarifa == "50" || porcentaje == 50)
                        doc.RetencionesIva.Add(new SriDocumentoRetencionIva { ValRetServ50 = valorRet });
                    else if (tarifa == "70" || porcentaje == 70)
                        doc.RetencionesIva.Add(new SriDocumentoRetencionIva { ValorRetServicios70 = valorRet });
                    else if (tarifa == "100" || porcentaje == 100)
                        doc.RetencionesIva.Add(new SriDocumentoRetencionIva { ValorRetServ100 = valorRet });
                }
            }

            return (doc, errores);
        }

        private SriDocumentoModificado ParsearDocumentoModificado(string numDocModificado, string codDocModificado)
        {
            var modificado = new SriDocumentoModificado();

            if (string.IsNullOrWhiteSpace(numDocModificado))
            {
                modificado.DocModificado = "01";
                modificado.EstabModificado = "001";
                modificado.PtoEmiModificado = "001";
                modificado.SecModificado = "000000001";
                modificado.AutModificado = new string('9', 37);
                return modificado;
            }

            string limpio = numDocModificado.Replace("-", "").Replace(" ", "");
            var digitos = new string(limpio.Where(char.IsDigit).ToArray());

            if (digitos.Length >= 15)
            {
                modificado.DocModificado = !string.IsNullOrEmpty(codDocModificado) && codDocModificado.Length >= 2
                    ? codDocModificado.Substring(0, 2) : "01";
                modificado.EstabModificado = digitos.Substring(0, 3);
                modificado.PtoEmiModificado = digitos.Substring(3, 3);
                modificado.SecModificado = digitos.Substring(6, 9);
                modificado.AutModificado = digitos.Length >= 37 ? digitos : new string('9', 37);
            }
            else if (digitos.Length >= 9)
            {
                modificado.DocModificado = "01";
                modificado.EstabModificado = "001";
                modificado.PtoEmiModificado = "001";
                modificado.SecModificado = digitos.PadLeft(9, '0');
                modificado.AutModificado = new string('9', 37);
            }
            else
            {
                modificado.DocModificado = "01";
                modificado.EstabModificado = "001";
                modificado.PtoEmiModificado = "001";
                modificado.SecModificado = "000000001";
                modificado.AutModificado = new string('9', 37);
            }

            return modificado;
        }

        private int ObtenerEmpresaId(string ruc)
        {
            var empresa = _context.Empresas.FirstOrDefault(e => e.Ruc == ruc);
            if (empresa != null) return empresa.Id;

            var nueva = new Empresa { Ruc = ruc, RazonSocial = "POR DEFINIR" };
            _context.Empresas.Add(nueva);
            _context.SaveChanges();
            return nueva.Id;
        }

        private string ObtenerTipoIdentificacion(string identificacion)
        {
            if (identificacion.Length == 13 && identificacion.EndsWith("001")) return "04";
            if (identificacion.Length == 10) return "05";
            if (identificacion.Length == 13) return "04";
            return "06";
        }

        private string NormalizarCodigo(string valor, int digitos)
        {
            if (string.IsNullOrEmpty(valor)) return new string('0', digitos);
            return valor.PadLeft(digitos, '0').Substring(0, digitos);
        }

        private string NormalizarSecuencial(string valor)
        {
            if (string.IsNullOrEmpty(valor)) return "000000001";
            var digitos = new string(valor.Where(char.IsDigit).ToArray());
            return digitos.PadLeft(9, '0').Substring(0, 9);
        }
    }
}
