using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AtsManager.Models
{
    // --- CLASE SOPORTE: CargaLote ---
    public class CargaLote
    {
        public int Id { get; set; }
        public int Anio { get; set; }
        public int Mes { get; set; }
        public string TipoArchivo { get; set; } = string.Empty;
        public DateTime FechaCarga { get; set; } = DateTime.Now;
        public string NombreArchivo { get; set; } = string.Empty;
        public int TotalRegistros { get; set; }
    }

    /// <summary>
    /// Modelo para almacenar los registros de Compras para el ATS.
    /// </summary>
    public class Compra
    {
        // --- CLAVE PRIMARIA ---
        public int Id { get; set; }

        // --- INFORMACIÓN DE CONTROL / AUDITORÍA ---
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
        public string Estab { get; set; } // Ejemplo: "001"
        public string PtoEmi { get; set; } // Ejemplo: "002"
        public string Secuencial { get; set; } // Ejemplo: "000000615"

        // 3. COMPROBANTE
        [MaxLength(2)]
        public string TipoComprobante { get; set; } = string.Empty;
        public DateTime? FechaRegistro { get; set; }
        public DateTime? FechaEmision { get; set; }

        [MaxLength(15)]
        public string NumComprobante { get; set; } = string.Empty;

        [MaxLength(49)]
        public string Autorizacion { get; set; } = string.Empty;

        // --- BASES IMPONIBLES (APLICAR '?') ---
        // Si la columna en la DB permite NULL, el tipo en C# DEBE ser anulable.
        public decimal? BaseNoGraIva { get; set; }    // <-- CORREGIDO
        public decimal? BaseImpGrav { get; set; }     // <-- CORREGIDO
        public decimal BaseImponible { get; set; } // Podría ser NO NULL
        public decimal? BaseImpExe { get; set; }      // <-- CORREGIDO
        public decimal? MontoIce { get; set; }        // <-- CORREGIDO
        public decimal? MontoIva { get; set; }        // <-- CORREGIDO (CAUSA FRECUENTE DEL ERROR)
        public decimal? MontoTotal { get; set; }

        // --- RETENCIONES IVA (TODOS DEBEN SER ANULABLES) ---
        public decimal? ValRetBien10 { get; set; }    // <-- CORREGIDO (CAUSA FRECUENTE DEL ERROR)
        public decimal? ValRetServ20 { get; set; }    // <-- CORREGIDO
        public decimal? ValorRetBienes { get; set; }  // <-- CORREGIDO
        public decimal? ValRetServ50 { get; set; }    // <-- CORREGIDO
        public decimal? ValorRetServicios { get; set; } // <-- CORREGIDO
        public decimal? ValRetServ100 { get; set; }   // <-- CORREGIDO
        public decimal? ValorRetencionNc { get; set; }// <-- CORREGIDO

        // --- RETENCIÓN RENTA (TODOS LOS MONTOS DEBEN SER ANULABLES) ---
        public decimal? BaseImpAir { get; set; }      // <-- CORREGIDO
        public string CodRetAir { get; set; } = "332";
        public decimal? PorcentajeAir { get; set; }   // <-- CORREGIDO
        public decimal? ValRetAir { get; set; }       // <-- CORREGIDO (CAUSA FRECUENTE DEL ERROR)

        // --- PAGO AL EXTERIOR (Mantener como están si son NOT NULL en la DB, si no, usar '?' en los string) ---
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
        public string FormaPago { get; set; } = "01";
    }
    /// <summary>
    /// Modelo para almacenar los registros de Ventas para el ATS.
    /// </summary>
    public class Venta
    {
        // --- CLAVE PRIMARIA ---
        public int Id { get; set; }

        // --- INFORMACIÓN DE CONTROL / AUDITORÍA ---
        public short? Anio { get; set; }
        public short? Mes { get; set; }
        public DateTime FechaCreacion { get; set; }
        public string UsuarioCreacion { get; set; } = "SYSTEM";
        public int? CargaLoteId { get; set; }
        public CargaLote? CargaLote { get; set; }

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
        // Son opcionales si la venta es exenta o tiene tarifa 0, por lo que deben ser '?'
        public decimal? BaseImponible { get; set; } // Campo principal, puede ser NO NULL
        public decimal? BaseImpGrav { get; set; }     // <-- CORREGIDO
        public decimal? BaseNoGraIva { get; set; }    // <-- CORREGIDO
        public decimal? MontoIva { get; set; }        // <-- CORREGIDO
        public decimal? MontoIce { get; set; }        // <-- CORREGIDO
        public decimal? MontoTotal { get; set; }    // Campo principal, puede ser NO NULL

        // 1. FORMA PAGO (No necesita corrección de nulabilidad si es siempre "01")
        [MaxLength(2)]
        public string FormaPago { get; set; } = "01";

        // --- RETENCIONES (TODOS DEBEN SER ANULABLES) ---

        // 2. CORRECCIÓN de nombre y TIPO: 'ValorRetiva' a 'valRetIVA'
        public decimal? valRetIVA { get; set; } // <-- CORREGIDO a decimal?

        // 3. CORRECCIÓN de nombre y TIPO: 'ValorRetRenta' a 'valRetRenta'
        public decimal? valRetRenta { get; set; } // <-- CORREGIDO a decimal?

        // 4. NÚMERO DE RETENCIÓN (String ya es anulable por defecto con C# 8+, pero se explicita)
        [MaxLength(15)]
        public string? NumRetencion { get; set; }

        // 5. AUTORIZACIÓN DE RETENCIÓN
        [MaxLength(49)]
        public string? AutorizacionRetencion { get; set; }

        // 6. FECHA DE RETENCIÓN
        public DateTime? FechaRetencion { get; set; } // <-- CORREGIDO a DateTime?
    }
}