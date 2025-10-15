using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Data
{
    public class ApplicationDbContext : DbContext // Heredar de DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) // Constructor
            : base(options) { }

        // Mapeo de entidades con los nombres correctos según la base de datos
        public DbSet<Usuario> Usuario { get; set; }
        public DbSet<Turno> Turno { get; set; }
        public DbSet<ProductoServicio> ProductosServicios { get; set; }
        public DbSet<Atencion> Atencion { get; set; }
        public DbSet<DetalleAtencion> DetalleAtencion { get; set; }
        public DbSet<EstadoTurno> EstadoTurno { get; set; }
        public DbSet<Imagen> Imagen { get; set; }
        public DbSet<Rol> Rol { get; set; }
        public DbSet<Pago> Pagos { get; set; }

        // Nuevas tablas para cierre de caja
        public DbSet<CierreDiario> CierresDiarios { get; set; }
        public DbSet<CierreDiarioPago> CierresDiariosPagos { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder) // Mapeo de tablas y relaciones
        {
            base.OnModelCreating(modelBuilder);

            // Mapear tablas existentes
            modelBuilder.Entity<Usuario>().ToTable("usuario");
            modelBuilder.Entity<Turno>().ToTable("turno");
            modelBuilder.Entity<ProductoServicio>().ToTable("productos_servicios");
            modelBuilder.Entity<Atencion>().ToTable("atencion");
            modelBuilder.Entity<DetalleAtencion>().ToTable("detalle_atencion");
            modelBuilder.Entity<EstadoTurno>().ToTable("estado_turno");
            modelBuilder.Entity<Imagen>().ToTable("imagen");
            modelBuilder.Entity<Rol>().ToTable("rol");
            modelBuilder.Entity<Pago>().ToTable("pago");

            // Configuración de relaciones para Pago
            modelBuilder
                .Entity<Pago>()
                .HasOne(p => p.Atencion)
                .WithMany()
                .HasForeignKey(p => p.AtencionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Pago>().Property(p => p.MetodoPago).HasConversion<string>();

            // Mapear nuevas tablas cierre de caja con nombres explícitos
            modelBuilder.Entity<CierreDiario>().ToTable("cierre_diario");
            modelBuilder.Entity<CierreDiarioPago>().ToTable("cierre_diario_pago");

            // Configurar relación uno a muchos entre cierre_diario y cierre_diario_pago
            modelBuilder
                .Entity<CierreDiario>()
                .HasMany(c => c.Pagos)
                .WithOne(p => p.CierreDiario)
                .HasForeignKey(p => p.CierreDiarioId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
