using System;
using System.ComponentModel.DataAnnotations;

namespace AtsManager.Models
{
    public class NCCompra
    {
        [Key]
        public int Id { get; set; }

        public string ClaveAcceso { get; set; }
        public DateTime FechaEmision { get; set; }
        public DateTime FechaAutorizacion { get; set; }

        public string SerieComprobante { get; set; }
        public string NumeroDocumentoModificado { get; set; }

        public string RucEmisor { get; set; }
        public string RazonSocialEmisor { get; set; }

        public decimal ValorSinImpuestos { get; set; }
        public decimal IVA { get; set; }
        public decimal Total { get; set; }

        public int Anio { get; set; }
        public int Mes { get; set; }

        public DateTime FechaRegistro { get; set; }
        public string UsuarioCreacion { get; set; }
        public DateTime FechaCreacion { get; set; }
    }
}
