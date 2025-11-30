using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Data
{
    public class ApplicationDbContext : DbContext // Heredar de DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) // Constructor
            : base(options) { }

        // Mapeo de entidades con los nombres correctos según la base de datos
        public DbSet<Usuario> Usuario { get; set; } = null!;
        public DbSet<Turno> Turno { get; set; } = null!;
        public DbSet<ProductoServicio> ProductosServicios { get; set; } = null!;
        public DbSet<Atencion> Atencion { get; set; } = null!;
        public DbSet<DetalleAtencion> DetalleAtencion { get; set; } = null!;
        public DbSet<EstadoTurno> EstadoTurno { get; set; } = null!;
        public DbSet<Imagen> Imagen { get; set; } = null!;
        public DbSet<Rol> Rol { get; set; } = null!;
        public DbSet<Pago> Pagos { get; set; } = null!;
        public DbSet<VerificacionEmail> VerificacionEmail { get; set; } = null!;
        public DbSet<ReseteoContrasena> ReseteoContrasena { get; set; } = null!;

        // Nuevas tablas para cierre de caja
        public DbSet<CierreDiario> CierresDiarios { get; set; } = null!;
        public DbSet<CierreDiarioPago> CierresDiariosPagos { get; set; } = null!;
        public DbSet<BloqueoHorario> BloqueosHorario { get; set; } = null!;
        public DbSet<ConfiguracionTurno> ConfiguracionTurno { get; set; } = null!; // Agregar DbSet para ConfiguracionTurno
        public DbSet<DisponibilidadBarbero> DisponibilidadBarbero { get; set; } = null!;
        public DbSet<ConfiguracionSistema> ConfiguracionesSistema { get; set; } = null!;

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
            modelBuilder.Entity<VerificacionEmail>().ToTable("verificacion_email");
            modelBuilder.Entity<ReseteoContrasena>().ToTable("reseteo_contrasena");

            // Configuración de VerificacionEmail
            modelBuilder.Entity<VerificacionEmail>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UsuarioId).IsRequired();
                entity.Property(e => e.CodeHash).HasColumnType("binary(32)").IsRequired(false);
                entity.Property(e => e.Expiracion).IsRequired();
                entity.Property(e => e.CodeExpiresAt).IsRequired(false);
                entity.Property(e => e.FechaCreacion).IsRequired();
                entity.Property(e => e.Usado).HasDefaultValue(false);
                entity.Property(e => e.AttemptCount).HasDefaultValue(0);

                // Índices
                entity.HasIndex(e => e.CodeHash).HasDatabaseName("IX_VerificacionEmail_CodeHash");

                // FK a usuario
                entity
                    .HasOne<Usuario>()
                    .WithMany()
                    .HasForeignKey(e => e.UsuarioId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configuración de ReseteoContrasena
            modelBuilder.Entity<ReseteoContrasena>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UsuarioId).IsRequired();
                entity.Property(e => e.TokenHash).HasColumnType("binary(32)").IsRequired();
                entity.Property(e => e.FechaCreacion).IsRequired();
                entity.Property(e => e.FechaExpiracion).IsRequired();
                entity.Property(e => e.Usado).HasDefaultValue(false);

                // Índices
                entity.HasIndex(e => e.TokenHash).IsUnique();

                // FK a usuario
                entity
                    .HasOne<Usuario>()
                    .WithMany()
                    .HasForeignKey(e => e.UsuarioId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

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

            modelBuilder.Entity<BloqueoHorario>().ToTable("bloqueo_horario");
            modelBuilder.Entity<ConfiguracionTurno>().ToTable("configuracion_turno"); // Mapear tabla configuracion_turno
            modelBuilder.Entity<DisponibilidadBarbero>().ToTable("disponibilidad_barbero");
            modelBuilder.Entity<ConfiguracionSistema>().ToTable("configuracion_sistema");

            // Configurar índice único para Clave
            modelBuilder.Entity<ConfiguracionSistema>(entity =>
            {
                entity.HasIndex(e => e.Clave).IsUnique();
            });
        }
    }
}
