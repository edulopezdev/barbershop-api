using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using backend.Data;
using backend.Dtos;
using backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace backend.Services
{
    /// <summary>
    /// Servicio para obtener información consolidada del dashboard del cliente.
    /// Centraliza la lógica de negocio para recuperar turnos, servicios, horarios, ubicación, etc.
    /// </summary>
    public class ClienteDashboardService : IClienteDashboardService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ClienteDashboardService> _logger;

        public ClienteDashboardService(
            ApplicationDbContext context,
            ILogger<ClienteDashboardService> logger
        )
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Obtiene el dashboard completo del cliente.
        /// Agrupa: próximo turno, servicios, horarios, ubicación, promociones y resumen de atenciones.
        /// </summary>
        public async Task<ClienteDashboardDto> GetClienteDashboardAsync(int usuarioId)
        {
            try
            {
                var dashboard = new ClienteDashboardDto();

                // 1) Próximo turno del cliente
                dashboard.ProximoTurno = await GetProximoTurnoAsync(usuarioId);

                // 2) Servicios destacados
                dashboard.Servicios = await GetServiciosDestacadosAsync();

                // 3) Información de la barbería (dirección, contactos, horarios configurados)
                dashboard.Barberia = await GetBarberiaInfoAsync();

                // 6) Resumen de actividad del cliente
                dashboard.ResumenCliente = await GetResumenClienteAsync(usuarioId);

                // 7) Último servicio realizado por el cliente (detalle + producto/servicio)
                dashboard.UltimoServicio = await GetUltimoServicioAsync(usuarioId);

                return dashboard;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error al obtener dashboard para usuario {UsuarioId}",
                    usuarioId
                );
                throw;
            }
        }

        /// <summary>
        /// Obtiene el próximo turno del cliente.
        /// </summary>
        private async Task<ProximoTurnoDto?> GetProximoTurnoAsync(int usuarioId)
        {
            // Excluir turnos ya vencidos: devolver sólo turnos estrictamente en el futuro.
            // Usamos `DateTime.Now` para comparar con la misma referencia horaria local
            // que probablemente se usa en la base de datos.
            return await _context
                .Turno.Where(t => t.ClienteId == usuarioId && t.FechaHora > DateTime.Now)
                .OrderBy(t => t.FechaHora)
                .Select(t => new ProximoTurnoDto
                {
                    Id = t.Id,
                    FechaHora = t.FechaHora,
                    EstadoId = t.EstadoId,
                    BarberoId = t.BarberoId,
                    ClienteId = t.ClienteId,
                    BarberoNombre = t.Barbero != null ? t.Barbero.Nombre : null,
                    EstadoNombre = t.Estado != null ? t.Estado.Nombre : null,
                })
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Obtiene los servicios destacados (activos, no almacenables).
        /// </summary>
        private async Task<List<ServicioResumenDto>> GetServiciosDestacadosAsync()
        {
            return await _context
                .ProductosServicios.Where(p => !p.EsAlmacenable && p.Activo)
                .Select(p => new ServicioResumenDto
                {
                    Id = p.Id,
                    Nombre = p.Nombre,
                    Precio = p.Precio,
                })
                .Take(8)
                .ToListAsync();
        }

        /// <summary>
        /// Obtiene la información de la barbería desde ConfiguracionesSistema.
        /// Lee claves como NOMBRE_BARBERIA, UBICACION_DIRECCION, UBICACION_MAPS_URL, WHATSAPP_CONTACTO, EMAIL_CONTACTO, INSTAGRAM_URL
        /// y las claves de horarios y políticas: HORARIO_*, ULTIMO_TURNO_*, DIAS_LABORALES_CORTADO, DOMINGO_CERRADO, ANTICIPACION_MINIMA_HORAS, MAX_TURNOS_POR_CLIENTE_POR_DIA, DURACION_TURNO_MINUTOS, CANCELACION_MINIMA_HORAS, OBSERVACION_CANCELACION_HABILITADA.
        /// </summary>
        private async Task<BarberiaInfoDto> GetBarberiaInfoAsync()
        {
            // Solo leer las claves que exponemos al cliente (evitamos publicar políticas o flags internas)
            var keys = new[]
            {
                "NOMBRE_BARBERIA",
                "UBICACION_DIRECCION",
                "UBICACION_MAPS_URL",
                "WHATSAPP_CONTACTO",
                "EMAIL_CONTACTO",
                "INSTAGRAM_URL",
                "HORARIO_MATUTINO_INICIO",
                "HORARIO_MATUTINO_FIN",
                "HORARIO_VESPERTINO_INICIO",
                "HORARIO_VESPERTINO_FIN",
            };

            var configs = await _context
                .ConfiguracionesSistema.Where(c => keys.Contains(c.Clave))
                .ToDictionaryAsync(c => c.Clave, c => c.Valor);

            var dto = new BarberiaInfoDto
            {
                Nombre = configs.GetValueOrDefault("NOMBRE_BARBERIA"),
                Direccion = configs.GetValueOrDefault("UBICACION_DIRECCION"),
                MapsUrl = configs.GetValueOrDefault("UBICACION_MAPS_URL"),
                Whatsapp = configs.GetValueOrDefault("WHATSAPP_CONTACTO"),
                Email = configs.GetValueOrDefault("EMAIL_CONTACTO"),
                Instagram = configs.GetValueOrDefault("INSTAGRAM_URL"),

                HorarioMatutinoInicio = configs.GetValueOrDefault("HORARIO_MATUTINO_INICIO"),
                HorarioMatutinoFin = configs.GetValueOrDefault("HORARIO_MATUTINO_FIN"),
                HorarioVespertinoInicio = configs.GetValueOrDefault("HORARIO_VESPERTINO_INICIO"),
                HorarioVespertinoFin = configs.GetValueOrDefault("HORARIO_VESPERTINO_FIN"),
            };

            return dto;
        }

        // Nota: ya no se exponen promociones ni horarios por barbero en el dashboard.

        /// <summary>
        /// Obtiene el resumen de actividad del cliente (conteo de atenciones y última atención).
        /// </summary>
        private async Task<ResumenClienteDto?> GetResumenClienteAsync(int usuarioId)
        {
            var atencionesCount = await _context.Atencion.CountAsync(a => a.ClienteId == usuarioId);

            var ultimaAtencion = await _context
                .Atencion.Where(a => a.ClienteId == usuarioId)
                .OrderByDescending(a => a.Fecha)
                .Select(a => new UltimaAtencionDto { Id = a.Id, Fecha = a.Fecha })
                .FirstOrDefaultAsync();

            return new ResumenClienteDto
            {
                AtencionesCount = atencionesCount,
                UltimaAtencion = ultimaAtencion,
            };
        }

        /// <summary>
        /// Obtiene el último servicio (detalle de atención) para un cliente,
        /// incluyendo información del ProductoServicio (nombre, descripción).
        /// Realiza un join entre DetalleAtencion y Atencion para filtrar por ClienteId.
        /// </summary>
        private async Task<UltimoServicioDto?> GetUltimoServicioAsync(int usuarioId)
        {
            var query =
                from d in _context.DetalleAtencion
                join a in _context.Atencion on d.AtencionId equals a.Id
                where a.ClienteId == usuarioId
                orderby a.Fecha descending, d.Id descending
                select new UltimoServicioDto
                {
                    DetalleAtencionId = d.Id,
                    AtencionId = d.AtencionId,
                    ProductoServicioId = d.ProductoServicioId,
                    Nombre = d.ProductoServicio != null ? d.ProductoServicio.Nombre : null,
                    Descripcion =
                        d.ProductoServicio != null ? d.ProductoServicio.Descripcion : null,
                    Cantidad = d.Cantidad,
                    PrecioUnitario = d.PrecioUnitario,
                };

            return await query.FirstOrDefaultAsync();
        }
    }
}
