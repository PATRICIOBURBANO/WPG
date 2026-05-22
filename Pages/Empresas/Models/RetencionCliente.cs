using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AtsManager.Pages.Empresas.Models
{
    public class RetencionCliente
    {
        [Key]
        public int Id { get; set; }

        public int? CargaLoteId { get; set; }
        [ForeignKey("CargaLoteId")]
        public virtual CargaLote? CargaLote { get; set; }

        public string RucEmpresa { get; set; } = string.Empty;
        public short Anio { get; set; }
        public short Mes { get; set; }
        public DateTime FechaCreacion { get; set; }
        public string UsuarioCreacion { get; set; } = "Sistema";

        // Numero de retención
        [StringLength(17)]
        public string NumRetencionCompleto { get; set; } = string.Empty;
        [StringLength(15)]
        public string NumRetencion { get; set; } = string.Empty;
        [StringLength(49)]
        public string AutorizacionRetencion { get; set; } = string.Empty;

        // Fecha de la retención
        public DateTime? FechaRetencion { get; set; }

        // Emisor de la retención (quien nos retiene)
        [StringLength(13)]
        public string RucEmisor { get; set; } = string.Empty;
        [StringLength(500)]
        public string RazonSocialEmisor { get; set; } = string.Empty;

        // Documento que afecta (factura)
        [StringLength(17)]
        public string DocAfectado { get; set; } = string.Empty;
        public DateTime? FechaDocAfectado { get; set; }

        // Cliente (quien recibe la retención - nuestra empresa)
        [StringLength(13)]
        public string IdCliente { get; set; } = string.Empty;
        [StringLength(500)]
        public string RazonSocialCliente { get; set; } = string.Empty;

        // Base IVA
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? BaseImpGrav { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? MontoIva { get; set; }

        // Porcentaje Retención IVA
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? PorcentajeIva { get; set; }

        // Base Renta
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? BaseImpAir { get; set; }

        // Retención IVA
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? ValRetBien10 { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? ValRetServ20 { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? ValorRetBienes { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? ValRetServ50 { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? ValorRetServicios { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? ValRetServ100 { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? ValRetIva { get; set; }

        // Retención Renta
        [StringLength(10)]
        public string CodRetAir { get; set; } = "332";
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? PorcentajeAir { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? ValRetRenta { get; set; }

        // Totales
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? TotalRetencion { get; set; }
    }
}