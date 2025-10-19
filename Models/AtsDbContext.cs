using Microsoft.EntityFrameworkCore;

namespace AtsManager.Models
{
    public class AtsDbContext : DbContext
    {
        public AtsDbContext(DbContextOptions<AtsDbContext> options) : base(options) { }

        public DbSet<CargaLote> CargasLotes { get; set; }
        public DbSet<Compra> Compras { get; set; }
        public DbSet<Venta> Ventas { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // --- CargasLotes: Garantiza la unicidad de mes/año/tipo ---
            modelBuilder.Entity<CargaLote>()
                .HasIndex(c => new { c.Anio, c.Mes, c.TipoArchivo })
                .IsUnique();

            // --- Compras: Definir precisión para decimales (COMPLETA) ---
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

            // Nuevas propiedades añadidas
            modelBuilder.Entity<Venta>()
                .Property(v => v.FormaPago).HasMaxLength(2).IsRequired(false);
            modelBuilder.Entity<Venta>()
                .Property(v => v.NumRetencion).HasMaxLength(15).IsRequired(false);
            modelBuilder.Entity<Venta>()
                .Property(v => v.AutorizacionRetencion).HasMaxLength(49).IsRequired(false);
            modelBuilder.Entity<Venta>()
                .Property(v => v.FechaRetencion).HasColumnType("date").IsRequired(false);
        }
    }
}