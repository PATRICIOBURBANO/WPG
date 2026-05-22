using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AtsManager.Pages.Empresas.Models
{
    public enum ModuloAts
    {
        COMPRA,
        VENTA,
        EXPORTACION,
        ANULADO,
        LIQUIDACION
    }

    public enum DireccionDocumento
    {
        EMITIDO,
        RECIBIDO
    }

    public enum EstadoDocumento
    {
        PENDIENTE,
        PROCESADO,
        VALIDADO,
        ERROR,
        ANULADO
    }

    public enum EstadoParseo
    {
        PENDIENTE,
        PROCESADO,
        ERROR
    }

    public enum TipoArchivoSri
    {
        XML,
        PDF
    }

    public enum TipoDocumentoSri
    {
        FACTURA,
        NOTA_CREDITO,
        NOTA_DEBITO,
        RETENCION,
        LIQUIDACION,
        GUIA_REMISION
    }

    public enum EstadoAutorizacion
    {
        AUTORIZADO,
        NO_AUTORIZADO,
        PENDIENTE
    }

    public enum AmbienteSri
    {
        PRODUCCION = 1,
        PRUEBAS = 2
    }

    public class SriRawFile
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int EmpresaId { get; set; }
        [ForeignKey("EmpresaId")]
        public virtual Empresa? Empresa { get; set; }

        public short PeriodoAnio { get; set; }
        public short PeriodoMes { get; set; }

        public TipoArchivoSri TipoArchivo { get; set; }
        public TipoDocumentoSri TipoDocumentoSri { get; set; }

        [MaxLength(49)]
        public string? ClaveAcceso { get; set; }

        [MaxLength(49)]
        public string? NumeroAutorizacion { get; set; }

        [MaxLength(255)]
        public string NombreArchivo { get; set; } = string.Empty;

        [MaxLength(500)]
        public string RutaArchivo { get; set; } = string.Empty;

        [MaxLength(64)]
        public string? HashArchivo { get; set; }

        public DateTime FechaDescarga { get; set; } = DateTime.Now;

        public EstadoParseo EstadoParseo { get; set; } = EstadoParseo.PENDIENTE;

        [MaxLength(1000)]
        public string? MensajeError { get; set; }

        public int? XmlPayloadId { get; set; }
        [ForeignKey("XmlPayloadId")]
        public virtual SriXmlPayload? XmlPayload { get; set; }

        public DateTime FechaCreacion { get; set; } = DateTime.Now;
        [MaxLength(50)]
        public string UsuarioCreacion { get; set; } = "SYSTEM";
    }

    public class SriXmlPayload
    {
        [Key]
        public int Id { get; set; }

        public int RawFileId { get; set; }
        [ForeignKey("RawFileId")]
        public virtual SriRawFile? RawFile { get; set; }

        public string XmlContenido { get; set; } = string.Empty;

        public EstadoAutorizacion EstadoAutorizacion { get; set; }
        public AmbienteSri? Ambiente { get; set; }

        public DateTime? FechaAutorizacion { get; set; }

        public DateTime FechaCreacion { get; set; } = DateTime.Now;
    }

    public class SriDocumento
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int EmpresaId { get; set; }
        [ForeignKey("EmpresaId")]
        public virtual Empresa? Empresa { get; set; }

        public short PeriodoAnio { get; set; }
        public short PeriodoMes { get; set; }

        public ModuloAts ModuloAts { get; set; }
        public DireccionDocumento DireccionDocumento { get; set; }

        [MaxLength(2)]
        public string TipoComprobanteCodigo { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? TipoComprobanteNombre { get; set; }

        public bool EsElectronico { get; set; } = true;
        public EstadoDocumento EstadoDocumento { get; set; } = EstadoDocumento.PENDIENTE;

        [MaxLength(49)]
        public string? ClaveAcceso { get; set; }

        [MaxLength(49)]
        public string? NumeroAutorizacion { get; set; }

        [MaxLength(3)]
        public string Establecimiento { get; set; } = "001";

        [MaxLength(3)]
        public string PuntoEmision { get; set; } = "001";

        [MaxLength(9)]
        public string Secuencial { get; set; } = "000000001";

        [MaxLength(20)]
        public string NumeroDocumento { get; set; } = string.Empty;

        public DateTime? FechaEmision { get; set; }
        public DateTime? FechaAutorizacion { get; set; }
        public DateTime? FechaRegistroContable { get; set; }

        [MaxLength(13)]
        public string? RucEmisor { get; set; }

        [MaxLength(500)]
        public string? RazonSocialEmisor { get; set; }

        [MaxLength(13)]
        public string IdentificacionContraparte { get; set; } = string.Empty;

        [MaxLength(2)]
        public string TipoIdentificacionContraparte { get; set; } = string.Empty;

        [MaxLength(500)]
        public string RazonSocialContraparte { get; set; } = string.Empty;

        public bool ParteRelacionada { get; set; }

        [MaxLength(2)]
        public string? TipoProvTipoCli { get; set; }

        [MaxLength(2)]
        public string? CodSustento { get; set; }

        [MaxLength(2)]
        public string? PagoLocExt { get; set; }

        [MaxLength(2)]
        public string? TipoRegi { get; set; }

        [MaxLength(3)]
        public string? PaisEfecPagoGen { get; set; }

        [MaxLength(3)]
        public string? PaisEfecPagoParFis { get; set; }

        [MaxLength(500)]
        public string? DenopagoRegFis { get; set; }

        [MaxLength(3)]
        public string? PaisEfecPago { get; set; }

        [MaxLength(2)]
        public string? AplicaConvenioDobleTrib { get; set; }

        [MaxLength(2)]
        public string? PagoExtSujRetNorLeg { get; set; }

        [MaxLength(2)]
        public string? PagoRegFis { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? BaseNoGraIva { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? BaseImponible0 { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? BaseImpGrav { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? BaseImpExe { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? MontoIva { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? MontoIce { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? TotalSinImpuestos { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? ImporteTotal { get; set; }

        public int? OrigenRawFileId { get; set; }
        [ForeignKey("OrigenRawFileId")]
        public virtual SriRawFile? OrigenRawFile { get; set; }

        [MaxLength(1000)]
        public string? Observaciones { get; set; }

        public string? JsonExtras { get; set; }

        public DateTime FechaCreacion { get; set; } = DateTime.Now;
        [MaxLength(50)]
        public string UsuarioCreacion { get; set; } = "SYSTEM";

        public int? CargaLoteId { get; set; }
        [ForeignKey("CargaLoteId")]
        public virtual CargaLote? CargaLote { get; set; }

        public virtual ICollection<SriDocumentoRetencionRenta> RetencionesRenta { get; set; } = new List<SriDocumentoRetencionRenta>();
        public virtual ICollection<SriDocumentoRetencionIva> RetencionesIva { get; set; } = new List<SriDocumentoRetencionIva>();
        public virtual ICollection<SriDocumentoFormaPago> FormasPago { get; set; } = new List<SriDocumentoFormaPago>();
        public virtual SriDocumentoModificado? DocumentoModificado { get; set; }
        public virtual SriDocumentoReembolso? Reembolso { get; set; }
    }

    public class SriDocumentoRetencionRenta
    {
        [Key]
        public int Id { get; set; }

        public int DocumentoId { get; set; }
        [ForeignKey("DocumentoId")]
        public virtual SriDocumento? Documento { get; set; }

        [MaxLength(3)]
        public string CodRetAir { get; set; } = "332";

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? BaseImpAir { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? PorcentajeAir { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? ValRetAir { get; set; }

        public DateTime? FechaPagoDiv { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? ImRentaSoc { get; set; }

        [MaxLength(4)]
        public string? AnioUtDiv { get; set; }

        [MaxLength(20)]
        public string? NumCajBan { get; set; }

        [MaxLength(20)]
        public string? PrecCajBan { get; set; }
    }

    public class SriDocumentoRetencionIva
    {
        [Key]
        public int Id { get; set; }

        public int DocumentoId { get; set; }
        [ForeignKey("DocumentoId")]
        public virtual SriDocumento? Documento { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? ValRetBien10 { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? ValRetServ20 { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? ValorRetBienes30 { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? ValRetServ50 { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? ValorRetServicios70 { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? ValorRetServ100 { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? ValorRetencionNc { get; set; }
    }

    public class SriDocumentoFormaPago
    {
        [Key]
        public int Id { get; set; }

        public int DocumentoId { get; set; }
        [ForeignKey("DocumentoId")]
        public virtual SriDocumento? Documento { get; set; }

        [MaxLength(2)]
        public string FormaPagoCodigo { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? MontoAsignado { get; set; }

        public int Orden { get; set; }
    }

    public class SriDocumentoModificado
    {
        [Key]
        public int Id { get; set; }

        public int DocumentoId { get; set; }
        [ForeignKey("DocumentoId")]
        public virtual SriDocumento? Documento { get; set; }

        [MaxLength(2)]
        public string? DocModificado { get; set; }

        [MaxLength(3)]
        public string? EstabModificado { get; set; }

        [MaxLength(3)]
        public string? PtoEmiModificado { get; set; }

        [MaxLength(9)]
        public string? SecModificado { get; set; }

        [MaxLength(49)]
        public string? AutModificado { get; set; }

        public int? DocumentoRelacionadoId { get; set; }
        [ForeignKey("DocumentoRelacionadoId")]
        public virtual SriDocumento? DocumentoRelacionado { get; set; }
    }

    public class SriDocumentoReembolso
    {
        [Key]
        public int Id { get; set; }

        public int DocumentoId { get; set; }
        [ForeignKey("DocumentoId")]
        public virtual SriDocumento? Documento { get; set; }

        [MaxLength(2)]
        public string? TipoComprobanteReemb { get; set; }

        [MaxLength(2)]
        public string? TpIdProvReemb { get; set; }

        [MaxLength(13)]
        public string? IdProvReemb { get; set; }

        [MaxLength(3)]
        public string? EstablecimientoReemb { get; set; }

        [MaxLength(3)]
        public string? PuntoEmisionReemb { get; set; }

        [MaxLength(9)]
        public string? SecuencialReemb { get; set; }

        public DateTime? FechaEmisionReemb { get; set; }

        [MaxLength(49)]
        public string? AutorizacionReemb { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? BaseImponibleReemb { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? BaseImpGravReemb { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? BaseNoGraIvaReemb { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? BaseImpExeReemb { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? TotBasesImpReemb { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? MontoIceReemb { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? MontoIvaReemb { get; set; }
    }

    public class SriComprobanteRetencionEmitido
    {
        [Key]
        public int Id { get; set; }

        public int DocumentoId { get; set; }
        [ForeignKey("DocumentoId")]
        public virtual SriDocumento? Documento { get; set; }

        [MaxLength(3)]
        public string? EstabRetencion { get; set; }

        [MaxLength(3)]
        public string? PtoEmiRetencion { get; set; }

        [MaxLength(9)]
        public string? SecRetencion { get; set; }

        [MaxLength(49)]
        public string? AutRetencion { get; set; }

        public DateTime? FechaEmiRet { get; set; }
    }

    public class SriDocumentoAnulado
    {
        [Key]
        public int Id { get; set; }

        public int EmpresaId { get; set; }
        [ForeignKey("EmpresaId")]
        public virtual Empresa? Empresa { get; set; }

        public short PeriodoAnio { get; set; }
        public short PeriodoMes { get; set; }

        [MaxLength(2)]
        public string TipoComprobante { get; set; } = string.Empty;

        [MaxLength(3)]
        public string Establecimiento { get; set; } = "001";

        [MaxLength(3)]
        public string PuntoEmision { get; set; } = "001";

        [MaxLength(9)]
        public string SecuencialInicio { get; set; } = "000000001";

        [MaxLength(9)]
        public string SecuencialFin { get; set; } = "000000001";

        [MaxLength(49)]
        public string? Autorizacion { get; set; }

        public DateTime FechaCreacion { get; set; } = DateTime.Now;
    }

    public class CatTipoComprobante
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(2)]
        public string Codigo { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Nombre { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? NombreCorto { get; set; }

        public bool EsNotaCreditoDebito { get; set; }
        public bool RequiereDocModificado { get; set; }
    }

    public class CatTipoIdentificacion
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(2)]
        public string Codigo { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Nombre { get; set; } = string.Empty;
    }

    public class CatSustento
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(2)]
        public string Codigo { get; set; } = string.Empty;

        [MaxLength(200)]
        public string Descripcion { get; set; } = string.Empty;
    }

    public class CatFormaPago
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(2)]
        public string Codigo { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Nombre { get; set; } = string.Empty;

        public bool RequiereMonto { get; set; }
    }

    public class CatAir
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(3)]
        public string Codigo { get; set; } = string.Empty;

        [MaxLength(200)]
        public string Descripcion { get; set; } = string.Empty;

        [MaxLength(10)]
        public string? Porcentaje { get; set; }
    }

    public class CatTipoRegimenExterior
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(2)]
        public string Codigo { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Nombre { get; set; } = string.Empty;
    }

    public class CatPais
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(3)]
        public string Codigo { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Nombre { get; set; } = string.Empty;

        public bool EsParaisoFiscal { get; set; }
    }

    public class CatTipoProveedorCliente
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(2)]
        public string Codigo { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Nombre { get; set; } = string.Empty;
    }

    public class CatTipoEmision
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(1)]
        public string Codigo { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Nombre { get; set; } = string.Empty;
    }
}
