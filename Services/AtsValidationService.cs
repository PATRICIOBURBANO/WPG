using AtsManager.Pages.Empresas.Models;

namespace AtsManager.Services
{
    public class AtsValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();

        public void AddError(string error) => Errors.Add(error);
        public void AddWarning(string warning) => Warnings.Add(warning);
    }

    public interface IAtsValidationService
    {
        AtsValidationResult ValidarDocumento(SriDocumento documento);
        AtsValidationResult ValidarNotaCredito(SriDocumento documento);
        AtsValidationResult ValidarNotaDebito(SriDocumento documento);
    }

    public class AtsValidationService : IAtsValidationService
    {
        public AtsValidationResult ValidarDocumento(SriDocumento documento)
        {
            var result = new AtsValidationResult { IsValid = true };

            if (documento == null)
            {
                result.IsValid = false;
                result.AddError("Documento no puede ser null");
                return result;
            }

            if (documento.EmpresaId <= 0)
            {
                result.IsValid = false;
                result.AddError("EmpresaId es requerido");
            }

            if (string.IsNullOrWhiteSpace(documento.TipoComprobanteCodigo))
            {
                result.IsValid = false;
                result.AddError("Tipo de comprobante es requerido");
            }

            if (string.IsNullOrWhiteSpace(documento.IdentificacionContraparte))
            {
                result.IsValid = false;
                result.AddError("Identificación de contrapartida es requerida");
            }

            if (!documento.FechaEmision.HasValue)
            {
                result.IsValid = false;
                result.AddError("Fecha de emisión es requerida");
            }

            if (documento.ModuloAts == ModuloAts.COMPRA)
            {
                if (string.IsNullOrWhiteSpace(documento.CodSustento))
                {
                    result.AddWarning("Código de sustento no especificado para compra");
                }
            }

            bool esNotaCreditoDebito = documento.TipoComprobanteCodigo == "04" || documento.TipoComprobanteCodigo == "03";
            if (esNotaCreditoDebito)
            {
                var ncResult = documento.TipoComprobanteCodigo == "04"
                    ? ValidarNotaCredito(documento)
                    : ValidarNotaDebito(documento);

                result.Errors.AddRange(ncResult.Errors);
                result.Warnings.AddRange(ncResult.Warnings);
                if (!ncResult.IsValid) result.IsValid = false;
            }

            ValidarMontos(result, documento);

            return result;
        }

        public AtsValidationResult ValidarNotaCredito(SriDocumento documento)
        {
            var result = new AtsValidationResult { IsValid = true };

            if (documento.TipoComprobanteCodigo != "04")
            {
                result.AddError("Tipo de comprobante debe ser 04 para nota de crédito");
                result.IsValid = false;
                return result;
            }

            if (documento.DocumentoModificado == null)
            {
                result.IsValid = false;
                result.AddError("Nota de crédito requiere documento modificado (docModificado)");
            }
            else
            {
                var mod = documento.DocumentoModificado;

                if (string.IsNullOrWhiteSpace(mod.DocModificado))
                {
                    result.AddWarning("DocModificado (tipoComprobanteModificado) no especificado");
                }

                if (string.IsNullOrWhiteSpace(mod.EstabModificado))
                {
                    result.AddWarning("Establecimiento modificado no especificado");
                }

                if (string.IsNullOrWhiteSpace(mod.PtoEmiModificado))
                {
                    result.AddWarning("Punto de emisión modificado no especificado");
                }

                if (string.IsNullOrWhiteSpace(mod.SecModificado))
                {
                    result.AddWarning("Secuencial modificado no especificado");
                }

                string auth = mod.AutModificado ?? "";
                string authDigits = new string(auth.Where(char.IsDigit).ToArray());

                if (authDigits.Length < 37)
                {
                    result.AddWarning($"Autorización del documento modificado insuficiente ({authDigits.Length} dígitos). Se usará placeholder de 37 nueves.");
                }

                bool esSustento15 = documento.CodSustento == "15";
                var retencionesIva = documento.RetencionesIva.ToList();

                if (esSustento15 && documento.ModuloAts == ModuloAts.COMPRA)
                {
                    bool tieneRetencionNc = retencionesIva.Any(r => r.ValorRetencionNc.HasValue && r.ValorRetencionNc > 0);
                    if (!tieneRetencionNc)
                    {
                        result.AddWarning("Nota de crédito con sustento 15 debería tener valorRetencionNc en retenciones IVA");
                    }
                }
            }

            ValidarMontosPositivos(result, documento, "Nota de crédito");

            return result;
        }

        public AtsValidationResult ValidarNotaDebito(SriDocumento documento)
        {
            var result = new AtsValidationResult { IsValid = true };

            if (documento.TipoComprobanteCodigo != "03")
            {
                result.AddError("Tipo de comprobante debe ser 03 para nota de débito");
                result.IsValid = false;
                return result;
            }

            if (documento.DocumentoModificado == null)
            {
                result.IsValid = false;
                result.AddError("Nota de débito requiere documento modificado (docModificado)");
            }
            else
            {
                var mod = documento.DocumentoModificado;

                if (string.IsNullOrWhiteSpace(mod.AutModificado))
                {
                    result.AddWarning("Autorización del documento modificado no especificada");
                }
                else
                {
                    string auth = mod.AutModificado;
                    string authDigits = new string(auth.Where(char.IsDigit).ToArray());

                    if (authDigits.Length < 37)
                    {
                        result.AddWarning($"Autorización del documento modificado insuficiente ({authDigits.Length} dígitos). Se usará placeholder.");
                    }
                }
            }

            ValidarMontosPositivos(result, documento, "Nota de débito");

            return result;
        }

        private void ValidarMontos(AtsValidationResult result, SriDocumento documento)
        {
            if (documento.BaseImpGrav.HasValue && documento.BaseImpGrav < 0)
            {
                result.AddError("Base imponible gravada no puede ser negativa");
                result.IsValid = false;
            }

            if (documento.BaseNoGraIva.HasValue && documento.BaseNoGraIva < 0)
            {
                result.AddError("Base no grabada con IVA no puede ser negativa");
                result.IsValid = false;
            }

            if (documento.MontoIva.HasValue && documento.MontoIva < 0)
            {
                result.AddError("Monto IVA no puede ser negativo");
                result.IsValid = false;
            }

            if (documento.MontoIce.HasValue && documento.MontoIce < 0)
            {
                result.AddError("Monto ICE no puede ser negativo");
                result.IsValid = false;
            }

            if (documento.ImporteTotal.HasValue && documento.ImporteTotal < 0)
            {
                result.AddError("Importe total no puede ser negativo");
                result.IsValid = false;
            }
        }

        private void ValidarMontosPositivos(AtsValidationResult result, SriDocumento documento, string tipoDocumento)
        {
            if (documento.BaseImpGrav.HasValue && documento.BaseImpGrav < 0)
            {
                result.AddError($"{tipoDocumento}: Base gravada debe ser positiva");
                result.IsValid = false;
            }

            if (documento.BaseNoGraIva.HasValue && documento.BaseNoGraIva < 0)
            {
                result.AddError($"{tipoDocumento}: Base no gravada debe ser positiva");
                result.IsValid = false;
            }

            if (documento.MontoIva.HasValue && documento.MontoIva < 0)
            {
                result.AddError($"{tipoDocumento}: Monto IVA debe ser positivo");
                result.IsValid = false;
            }

            if (documento.ImporteTotal.HasValue && documento.ImporteTotal < 0)
            {
                result.AddError($"{tipoDocumento}: Importe total debe ser positivo");
                result.IsValid = false;
            }
        }
    }
}
