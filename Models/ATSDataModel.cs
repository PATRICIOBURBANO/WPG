using Microsoft.AspNetCore.Mvc;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema; // <--- AÑADIR ESTE USING

namespace AtsManager.Models
{
    // --- CLASE SOPORTE: CargaLote ---
    public class CargaLote
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // <--- CORRECCIÓN AÑADIDA
        public int Id { get; set; }
        public int Anio { get; set; }
        public int Mes { get; set; }
        public string TipoArchivo { get; set; } = string.Empty;
        public DateTime FechaCarga { get; set; } = DateTime.Now;
        public string NombreArchivo { get; set; } = string.Empty;
        public int TotalRegistros { get; set; }
        public string TipoDocumento { get; set; } = string.Empty; // <-- Corregido de la versión anterior
    }

    /// <summary>
    /// Modelo para almacenar los registros de Compras para el ATS.
    /// </summary>
    public class Compra
    {
        // --- CLAVE PRIMARIA ---
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // <--- CORRECCIÓN AÑADIDA
        public int Id { get; set; }

        // --- INFORMACIÓN DE CONTROL / AUDITORÍA ---
        
        public string RucEmpresa { get; set; } = string.Empty;
        public short? Anio { get; set; }
        public short? Mes { get; set; }
        public DateTime FechaCreacion { get; set; }
        public string UsuarioCreacion { get; set; } = "SYSTEM";
        public int? CargaLoteId { get; set; }
        public CargaLote? CargaLote { get; set; }

        // --- CAMPOS TRANSACCIONALES PRINCIPALES (SRI) ---
        [MaxLength(10)]
        public string CodigoCompra { get; set; } = string.Empty;

        // 1. SUSTENTO TRIBUTARIO
        [MaxLength(2)]
        public string CodSustento { get; set; } = string.Empty;

        // 2. PROVEEDOR
        [MaxLength(2)]
        public string TipoIdProveedor { get; set; } = string.Empty;
        [MaxLength(13)]
        public string IdProveedor { get; set; } = string.Empty;
        [MaxLength(500)]
        public string RazonSocialProveedor { get; set; } = string.Empty;
        [MaxLength(2)]
        public string TipoProveedor { get; set; } = "02";
        public bool ParteRelacionada { get; set; }
        public string Estab { get; set; } = string.Empty; // <-- CORRECCIÓN: Inicializado
        public string PtoEmi { get; set; } = string.Empty; // <-- CORRECCIÓN: Inicializado
        public string Secuencial { get; set; } = string.Empty; // <-- CORRECCIÓN: Inicializado

        // 3. COMPROBANTE
        [MaxLength(2)]
        public string TipoComprobante { get; set; } = string.Empty;
        public DateTime? FechaRegistro { get; set; }
        [Required] // Mantenemos el required
                   // ✅ CLAVE: Indica al Model Binder cómo debe interpretar la cadena entrante.
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy}", ApplyFormatInEditMode = true)]
        public DateTime? FechaEmision { get; set; }

        [MaxLength(15)]
        public string NumComprobante { get; set; } = string.Empty;

        [MaxLength(49)]
        public string Autorizacion { get; set; } = string.Empty;

        // --- DOCUMENTO MODIFICADO (para NC/ND) ---
        [MaxLength(2)]
        public string? TipoComprobanteModificado { get; set; }
        [MaxLength(3)]
        public string? EstablecimientoModificado { get; set; }
        [MaxLength(3)]
        public string? PuntoEmisionModificado { get; set; }
        [MaxLength(9)]
        public string? SecuencialModificado { get; set; }
        [MaxLength(49)]
        public string? AutorizacionModificada { get; set; }

        // --- BASES IMPONIBLES ---
        public decimal? BaseNoGraIva { get; set; }
        public decimal? BaseImpGrav { get; set; }
        public decimal? BaseImponible { get; set; }
        public decimal? BaseImpExe { get; set; }
        public decimal? MontoIce { get; set; }
        public decimal? MontoIva { get; set; }
        public decimal? MontoTotal { get; set; }

        // --- RETENCIONES IVA (TODOS DEBEN SER ANULABLES) ---
        public decimal? ValRetBien10 { get; set; }
        public decimal? ValRetServ20 { get; set; }
        public decimal? ValorRetBienes { get; set; }
        public decimal? ValRetServ50 { get; set; }
        public decimal? ValorRetServicios { get; set; }
        public decimal? ValRetServ100 { get; set; }
        public decimal? ValorRetencionNc { get; set; }

        // --- RETENCIÓN RENTA (TODOS LOS MONTOS DEBEN SER ANULABLES) ---
        public decimal? BaseImpAir { get; set; }
        public string CodRetAir { get; set; } = "332";
        public decimal? PorcentajeAir { get; set; }
        public decimal? ValRetAir { get; set; }

        // --- PAGO AL EXTERIOR ---
        [MaxLength(2)]
        public string PagoLocExt { get; set; } = "01";
        [MaxLength(2)]
        public string TipoRegi { get; set; } = string.Empty;
        [MaxLength(3)]
        public string PaisEfecPagoGen { get; set; } = string.Empty;
        [MaxLength(3)]
        public string PaisEfecPagoParFis { get; set; } = string.Empty;
        [MaxLength(500)]
        public string DenopagoRegFis { get; set; } = string.Empty;
        [MaxLength(3)]
        public string PaisEfecPago { get; set; } = string.Empty;
        [MaxLength(2)]
        public string AplicConvDobTrib { get; set; } = string.Empty;
        [MaxLength(2)]
        public string PagExtSujRetNorLeg { get; set; } = string.Empty;

        // --- FORMA DE PAGO ---
        [MaxLength(2)]
        public string FormaPago { get; set; } = "20";
    }
    /// <summary>
    /// Modelo para almacenar los registros de Ventas para el ATS.
    /// </summary>
    public class Venta
    {
        // --- CLAVE PRIMARIA ---
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        // --- INFORMACIÓN DE CONTROL / AUDITORÍA ---
        public string RucEmpresa { get; set; } = string.Empty;
        public short? Anio { get; set; }
        public short? Mes { get; set; }
        public DateTime FechaCreacion { get; set; }
        public string UsuarioCreacion { get; set; } = "SYSTEM";
        public int? CargaLoteId { get; set; }
        public CargaLote? CargaLote { get; set; }
        public string ClaveAcceso { get; set; } = string.Empty;

        // --- INFORMACIÓN DEL CLIENTE ---
        [MaxLength(2)]
        public string TipoIdCliente { get; set; } = string.Empty;
        [MaxLength(13)]
        public string IdCliente { get; set; } = string.Empty;
        [MaxLength(500)]
        public string RazonSocialCliente { get; set; } = string.Empty;
        public string? Estab { get; set; }
        public string? PtoEmi { get; set; }
        public string? Secuencial { get; set; }

        // --- INFORMACIÓN DEL COMPROBANTE ---
        public DateTime? FechaEmision { get; set; }
        [MaxLength(2)]
        public string TipoComprobante { get; set; } = string.Empty;
        [MaxLength(15)]
        public string NumComprobante { get; set; } = string.Empty;

        // --- BASES IMPONIBLES (Corregidas a anulables) ---
        public decimal? BaseImponible { get; set; }
        public decimal? BaseImpGrav { get; set; }
        public decimal? BaseNoGraIva { get; set; }
        public decimal? MontoIva { get; set; }
        public decimal? MontoIce { get; set; }
        public decimal? MontoTotal { get; set; }

        // 1. FORMA PAGO
        [MaxLength(2)]
        public string FormaPago { get; set; } = "20";

        // --- RETENCIONES (TODOS DEBEN SER ANULABLES) ---
        public decimal? valRetIVA { get; set; }
        public decimal? valRetRenta { get; set; }
        [MaxLength(15)]
        public string? NumRetencion { get; set; }
        [MaxLength(49)]
        public string? AutorizacionRetencion { get; set; }
        public DateTime? FechaRetencion { get; set; }
    }

    /// <summary>
    /// Modelo para almacenar Retenciones de Compras (recibidas de proveedores)
    /// </summary>
    public class RetencionCompra
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public string RucEmpresa { get; set; } = string.Empty;
        public short? Anio { get; set; }
        public short? Mes { get; set; }
        public DateTime FechaCreacion { get; set; }
        public string UsuarioCreacion { get; set; } = "SYSTEM";
        public int? CargaLoteId { get; set; }
        public CargaLote? CargaLote { get; set; }

        // Numero de retención
        [MaxLength(17)]
        public string NumRetencionCompleto { get; set; } = string.Empty;
        [MaxLength(15)]
        public string NumRetencion { get; set; } = string.Empty;
        [MaxLength(49)]
        public string Autorizacion { get; set; } = string.Empty;

        // Fecha de la retención
        public DateTime? FechaRetencion { get; set; }

        // Documento que afecta (factura)
        [MaxLength(17)]
        public string DocAfectado { get; set; } = string.Empty;
        public DateTime? FechaDocAfectado { get; set; }

        // Proveedor
        [MaxLength(13)]
        public string IdProveedor { get; set; } = string.Empty;
        [MaxLength(500)]
        public string RazonSocialProveedor { get; set; } = string.Empty;

        // Base IVA
        public decimal? BaseImpGrav { get; set; }
        public decimal? MontoIva { get; set; }

        // Base Renta
        public decimal? BaseImpAir { get; set; }

        // Retención IVA
        public decimal? ValRetBien10 { get; set; }
        public decimal? ValRetServ20 { get; set; }
        public decimal? ValorRetBienes { get; set; }
        public decimal? ValRetServ50 { get; set; }
        public decimal? ValorRetServicios { get; set; }
        public decimal? ValRetServ100 { get; set; }
        public decimal? ValRetIva { get; set; }

        // Retención Renta
        public string CodRetAir { get; set; } = "332";
        public decimal? PorcentajeAir { get; set; }
        public decimal? ValRetRenta { get; set; }

        // Totales
        public decimal? TotalRetencion { get; set; }
    }
}