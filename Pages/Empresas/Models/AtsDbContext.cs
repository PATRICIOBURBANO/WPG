using AtsManager.Models;
using AtsManager.Pages.NCCompras;
using Microsoft.EntityFrameworkCore;

namespace AtsManager.Pages.Empresas.Models
{
    public class AtsDbContext : DbContext
    {
        public AtsDbContext(DbContextOptions<AtsDbContext> options) : base(options) { }

        public DbSet<CargaLote> CargasLotes { get; set; }
        public DbSet<Compra> Compras { get; set; }
        public DbSet<NCCompra> NCCompras { get; set; }

        public DbSet<Venta> Ventas { get; set; }
        public DbSet<RetencionCliente> RetencionesClientes { get; set; }
        public DbSet<RetencionCompra> RetencionesCompras { get; set; }
        public DbSet<Empresa> Empresas { get; set; }

        public DbSet<SriRawFile> SriRawFiles { get; set; }
        public DbSet<SriXmlPayload> SriXmlPayloads { get; set; }
        public DbSet<SriDocumento> SriDocumentos { get; set; }
        public DbSet<SriDocumentoRetencionRenta> SriDocRetencionesRenta { get; set; }
        public DbSet<SriDocumentoRetencionIva> SriDocRetencionesIva { get; set; }
        public DbSet<SriDocumentoFormaPago> SriDocFormasPago { get; set; }
        public DbSet<SriDocumentoModificado> SriDocModificados { get; set; }
        public DbSet<SriDocumentoReembolso> SriDocReembolsos { get; set; }
        public DbSet<SriComprobanteRetencionEmitido> SriCompRetencionesEmitidos { get; set; }
        public DbSet<SriDocumentoAnulado> SriDocumentosAnulados { get; set; }

        public DbSet<CatTipoComprobante> CatTiposComprobante { get; set; }
        public DbSet<CatTipoIdentificacion> CatTiposIdentificacion { get; set; }
        public DbSet<CatSustento> CatSustentos { get; set; }
        public DbSet<CatFormaPago> CatFormasPago { get; set; }
        public DbSet<CatAir> CatAires { get; set; }
        public DbSet<CatTipoRegimenExterior> CatTiposRegimenExterior { get; set; }
        public DbSet<CatPais> CatPaises { get; set; }
        public DbSet<CatTipoProveedorCliente> CatTiposProveedorCliente { get; set; }
        public DbSet<CatTipoEmision> CatTiposEmision { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // --- CargasLotes: Ventas ---
            modelBuilder.Entity<CargaLote>()
                .HasMany<Venta>()
                .WithOne(v => v.CargaLote)
                .HasForeignKey(v => v.CargaLoteId)
                .OnDelete(DeleteBehavior.Cascade);

            // --- CargasLotes: Compras ---
            modelBuilder.Entity<CargaLote>()
                .HasMany<Compra>()
                .WithOne(c => c.CargaLote)
                .HasForeignKey(c => c.CargaLoteId)
                .OnDelete(DeleteBehavior.Cascade);

            // --- CargasLotes: RetencionesClientes ---
            modelBuilder.Entity<CargaLote>()
                .HasMany<RetencionCliente>()
                .WithOne(r => r.CargaLote)
                .HasForeignKey(r => r.CargaLoteId)
                .OnDelete(DeleteBehavior.Cascade);

            // --- CargasLotes: RetencionesCompras ---
            modelBuilder.Entity<CargaLote>()
                .HasMany<RetencionCompra>()
                .WithOne(r => r.CargaLote)
                .HasForeignKey(r => r.CargaLoteId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Compra>()
                .Property(c => c.BaseImponible).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<Compra>()
                .Property(c => c.BaseImpGrav).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<Compra>()
                .Property(c => c.BaseNoGraIva).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<Compra>()
                .Property(c => c.BaseImpExe).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<Compra>()
                .Property(c => c.MontoIva).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<Compra>()
                .Property(c => c.MontoIce).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<Compra>()
                .Property(c => c.MontoTotal).HasColumnType("decimal(18, 2)");

            // Nuevos campos para NC/ND y FormaPago
            modelBuilder.Entity<Compra>()
                .Property(c => c.TipoComprobanteModificado).HasMaxLength(2);
            modelBuilder.Entity<Compra>()
                .Property(c => c.EstablecimientoModificado).HasMaxLength(3);
            modelBuilder.Entity<Compra>()
                .Property(c => c.PuntoEmisionModificado).HasMaxLength(3);
            modelBuilder.Entity<Compra>()
                .Property(c => c.SecuencialModificado).HasMaxLength(9);
            modelBuilder.Entity<Compra>()
                .Property(c => c.AutorizacionModificada).HasMaxLength(49);
            modelBuilder.Entity<Compra>()
                .Property(c => c.FormaPago).HasMaxLength(2);

            // Retenciones IVA
            modelBuilder.Entity<Compra>()
                .Property(c => c.ValRetBien10).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<Compra>()
                .Property(c => c.ValRetServ20).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<Compra>()
                .Property(c => c.ValorRetBienes).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<Compra>()
                .Property(c => c.ValRetServ50).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<Compra>()
                .Property(c => c.ValorRetServicios).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<Compra>()
                .Property(c => c.ValRetServ100).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<Compra>()
                .Property(c => c.ValorRetencionNc).HasColumnType("decimal(18, 2)");

            // Retenciones Renta (AIR)
            modelBuilder.Entity<Compra>()
                .Property(c => c.BaseImpAir).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<Compra>()
                .Property(c => c.ValRetAir).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<Compra>()
                .Property(c => c.PorcentajeAir).HasColumnType("decimal(18, 2)");


            // --- Ventas: Definir precisión para decimales (BASE) ---
            modelBuilder.Entity<Venta>()
                .Property(v => v.BaseImponible).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<Venta>()
                .Property(v => v.BaseImpGrav).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<Venta>()
                .Property(v => v.BaseNoGraIva).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<Venta>()
                .Property(v => v.MontoIva).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<Venta>()
                .Property(v => v.MontoIce).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<Venta>()
                .Property(v => v.MontoTotal).HasColumnType("decimal(18, 2)");

            // --- Retenciones de Venta (CORRECCIÓN Y AÑADIDO) ---

            // CORRECCIÓN de nombres de las propiedades de retención a 'valRetIVA' y 'valRetRenta'
            modelBuilder.Entity<Venta>()
                .Property(v => v.valRetIVA).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<Venta>()
                .Property(v => v.valRetRenta).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<CargaLote>()
                .HasMany<Venta>()
                .WithOne(v => v.CargaLote)
                .HasForeignKey(v => v.CargaLoteId)
                .OnDelete(DeleteBehavior.Cascade);

            // Nuevas propiedades añadidas
            modelBuilder.Entity<Venta>()
                .Property(v => v.FormaPago).HasMaxLength(2).IsRequired(false);
            modelBuilder.Entity<Venta>()
                .Property(v => v.NumRetencion).HasMaxLength(15).IsRequired(false);
            modelBuilder.Entity<Venta>()
                .Property(v => v.AutorizacionRetencion).HasMaxLength(49).IsRequired(false);
            modelBuilder.Entity<Venta>()
                .Property(v => v.FechaRetencion).HasColumnType("date").IsRequired(false);

            // --- DocModificado para Ventas (NC) ---
            modelBuilder.Entity<Venta>()
                .Property(v => v.TipoComprobanteModificado).HasMaxLength(2).IsRequired(false);
            modelBuilder.Entity<Venta>()
                .Property(v => v.EstablecimientoModificado).HasMaxLength(3).IsRequired(false);
            modelBuilder.Entity<Venta>()
                .Property(v => v.PuntoEmisionModificado).HasMaxLength(3).IsRequired(false);
            modelBuilder.Entity<Venta>()
                .Property(v => v.SecuencialModificado).HasMaxLength(9).IsRequired(false);
            modelBuilder.Entity<Venta>()
                .Property(v => v.AutorizacionModificada).HasMaxLength(49).IsRequired(false);

            // --- Retenciones Compras ---
            modelBuilder.Entity<RetencionCompra>()
                .Property(r => r.BaseImpGrav).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<RetencionCompra>()
                .Property(r => r.MontoIva).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<RetencionCompra>()
                .Property(r => r.BaseImpAir).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<RetencionCompra>()
                .Property(r => r.ValRetBien10).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<RetencionCompra>()
                .Property(r => r.ValRetServ20).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<RetencionCompra>()
                .Property(r => r.ValorRetBienes).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<RetencionCompra>()
                .Property(r => r.ValRetServ50).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<RetencionCompra>()
                .Property(r => r.ValorRetServicios).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<RetencionCompra>()
                .Property(r => r.ValRetServ100).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<RetencionCompra>()
                .Property(r => r.ValRetIva).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<RetencionCompra>()
                .Property(r => r.PorcentajeAir).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<RetencionCompra>()
                .Property(r => r.ValRetRenta).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<RetencionCompra>()
                .Property(r => r.TotalRetencion).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<RetencionCompra>()
                .Property(r => r.NumRetencionCompleto).HasMaxLength(17);
            modelBuilder.Entity<RetencionCompra>()
                .Property(r => r.NumRetencion).HasMaxLength(15);
            modelBuilder.Entity<RetencionCompra>()
                .Property(r => r.Autorizacion).HasMaxLength(49);
            modelBuilder.Entity<RetencionCompra>()
                .Property(r => r.DocAfectado).HasMaxLength(17);
            modelBuilder.Entity<RetencionCompra>()
                .Property(r => r.CodRetAir).HasMaxLength(3);
            modelBuilder.Entity<RetencionCompra>()
                .Property(r => r.IdProveedor).HasMaxLength(13);
            modelBuilder.Entity<RetencionCompra>()
                .Property(r => r.RazonSocialProveedor).HasMaxLength(500);

            // --- Retenciones Clientes (Ventas) ---
            modelBuilder.Entity<RetencionCliente>()
                .Property(r => r.BaseImpGrav).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<RetencionCliente>()
                .Property(r => r.MontoIva).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<RetencionCliente>()
                .Property(r => r.BaseImpAir).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<RetencionCliente>()
                .Property(r => r.ValRetBien10).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<RetencionCliente>()
                .Property(r => r.ValRetServ20).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<RetencionCliente>()
                .Property(r => r.ValorRetBienes).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<RetencionCliente>()
                .Property(r => r.ValRetServ50).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<RetencionCliente>()
                .Property(r => r.ValorRetServicios).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<RetencionCliente>()
                .Property(r => r.ValRetServ100).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<RetencionCliente>()
                .Property(r => r.ValRetIva).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<RetencionCliente>()
                .Property(r => r.PorcentajeAir).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<RetencionCliente>()
                .Property(r => r.ValRetRenta).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<RetencionCliente>()
                .Property(r => r.TotalRetencion).HasColumnType("decimal(18, 2)");

            // --- Configuración de nuevas entidades Sri ---

            // SriDocumento - relaciones
            modelBuilder.Entity<SriDocumento>()
                .HasOne(d => d.Empresa)
                .WithMany()
                .HasForeignKey(d => d.EmpresaId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SriDocumento>()
                .HasOne(d => d.DocumentoModificado)
                .WithOne(dm => dm.Documento)
                .HasForeignKey<SriDocumentoModificado>(dm => dm.DocumentoId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SriDocumento>()
                .HasOne(d => d.Reembolso)
                .WithOne(r => r.Documento)
                .HasForeignKey<SriDocumentoReembolso>(r => r.DocumentoId)
                .OnDelete(DeleteBehavior.Cascade);

            // SriDocumentoRetencionRenta
            modelBuilder.Entity<SriDocumentoRetencionRenta>()
                .HasOne(r => r.Documento)
                .WithMany(d => d.RetencionesRenta)
                .HasForeignKey(r => r.DocumentoId)
                .OnDelete(DeleteBehavior.Cascade);

            // SriDocumentoRetencionIva
            modelBuilder.Entity<SriDocumentoRetencionIva>()
                .HasOne(r => r.Documento)
                .WithMany(d => d.RetencionesIva)
                .HasForeignKey(r => r.DocumentoId)
                .OnDelete(DeleteBehavior.Cascade);

            // SriDocumentoFormaPago
            modelBuilder.Entity<SriDocumentoFormaPago>()
                .HasOne(f => f.Documento)
                .WithMany(d => d.FormasPago)
                .HasForeignKey(f => f.DocumentoId)
                .OnDelete(DeleteBehavior.Cascade);

            // SriDocumentoModificado
            modelBuilder.Entity<SriDocumentoModificado>()
                .HasOne(dm => dm.DocumentoRelacionado)
                .WithMany()
                .HasForeignKey(dm => dm.DocumentoRelacionadoId)
                .OnDelete(DeleteBehavior.SetNull);

            // SriDocumentoReembolso
            modelBuilder.Entity<SriDocumentoReembolso>()
                .Property(r => r.BaseImponibleReemb).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<SriDocumentoReembolso>()
                .Property(r => r.BaseImpGravReemb).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<SriDocumentoReembolso>()
                .Property(r => r.BaseNoGraIvaReemb).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<SriDocumentoReembolso>()
                .Property(r => r.BaseImpExeReemb).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<SriDocumentoReembolso>()
                .Property(r => r.TotBasesImpReemb).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<SriDocumentoReembolso>()
                .Property(r => r.MontoIceReemb).HasColumnType("decimal(18, 2)");
            modelBuilder.Entity<SriDocumentoReembolso>()
                .Property(r => r.MontoIvaReemb).HasColumnType("decimal(18, 2)");

            // SriComprobanteRetencionEmitido
            modelBuilder.Entity<SriComprobanteRetencionEmitido>()
                .HasOne(r => r.Documento)
                .WithMany()
                .HasForeignKey(r => r.DocumentoId)
                .OnDelete(DeleteBehavior.Cascade);

            // SriDocumentoAnulado
            modelBuilder.Entity<SriDocumentoAnulado>()
                .HasOne(d => d.Empresa)
                .WithMany()
                .HasForeignKey(d => d.EmpresaId)
                .OnDelete(DeleteBehavior.Restrict);

            // SriRawFile
            modelBuilder.Entity<SriRawFile>()
                .HasOne(r => r.Empresa)
                .WithMany()
                .HasForeignKey(r => r.EmpresaId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SriRawFile>()
                .HasOne(r => r.XmlPayload)
                .WithOne(x => x.RawFile)
                .HasForeignKey<SriXmlPayload>(x => x.RawFileId)
                .OnDelete(DeleteBehavior.Cascade);

            // Catálogos
            modelBuilder.Entity<CatTipoComprobante>()
                .HasIndex(c => c.Codigo).IsUnique();
            modelBuilder.Entity<CatTipoIdentificacion>()
                .HasIndex(c => c.Codigo).IsUnique();
            modelBuilder.Entity<CatSustento>()
                .HasIndex(c => c.Codigo).IsUnique();
            modelBuilder.Entity<CatFormaPago>()
                .HasIndex(c => c.Codigo).IsUnique();
            modelBuilder.Entity<CatAir>()
                .HasIndex(c => c.Codigo).IsUnique();
            modelBuilder.Entity<CatTipoRegimenExterior>()
                .HasIndex(c => c.Codigo).IsUnique();
            modelBuilder.Entity<CatPais>()
                .HasIndex(c => c.Codigo).IsUnique();
            modelBuilder.Entity<CatTipoProveedorCliente>()
                .HasIndex(c => c.Codigo).IsUnique();
            modelBuilder.Entity<CatTipoEmision>()
                .HasIndex(c => c.Codigo).IsUnique();
        }
    }
}