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
    public class BarberoDashboardService : IBarberoDashboardService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<BarberoDashboardService> _logger;

        public BarberoDashboardService(
            ApplicationDbContext context,
            ILogger<BarberoDashboardService> logger
        )
        {
            _context = context;
            _logger = logger;
        }

        public async Task<BarberoDashboardDto> GetBarberoDashboardAsync(int barberoId)
        {
            var dto = new BarberoDashboardDto();

            // Próximo turno del barbero
            dto.ProximoTurno = await _context
                .Turno.Where(t => t.BarberoId == barberoId && t.FechaHora >= DateTime.Now)
                .OrderBy(t => t.FechaHora)
                .Select(t => new ProximoTurnoDto
                {
                    Id = t.Id,
                    FechaHora = t.FechaHora,
                    EstadoId = t.EstadoId,
                    BarberoId = t.BarberoId,
                    ClienteId = t.ClienteId,
                })
                .FirstOrDefaultAsync();

            // Resumen: turnos hoy, semana y total de atenciones
            var hoy = DateTime.Today;
            var finSemana = hoy.AddDays(7);

            dto.Resumen.TurnosHoy = await _context.Turno.CountAsync(t =>
                t.BarberoId == barberoId && t.FechaHora.Date == hoy
            );
            dto.Resumen.TurnosSemana = await _context.Turno.CountAsync(t =>
                t.BarberoId == barberoId && t.FechaHora.Date >= hoy && t.FechaHora.Date <= finSemana
            );
            dto.Resumen.TotalAtenciones = await _context.Atencion.CountAsync(a =>
                a.BarberoId == barberoId
            );

            // Ingresos últimos 30 días (suma de cantidad * precio unitario)
            var desde30 = DateTime.Now.AddDays(-30);
            var ingresos = await (
                from a in _context.Atencion
                join d in _context.DetalleAtencion on a.Id equals d.AtencionId
                where a.BarberoId == barberoId && a.Fecha >= desde30
                select d.Cantidad * d.PrecioUnitario
            ).SumAsync();

            dto.Resumen.Ingresos30Dias = ingresos;

            // Nota: eliminada la sección de "ServiciosTop" según solicitud; no se expone en el DTO.

            // Últimas atenciones (últimas 5) con detalles
            var ultimas = await _context
                .Atencion.Where(a => a.BarberoId == barberoId)
                .OrderByDescending(a => a.Fecha)
                .Take(5)
                .Select(a => new UltimaAtencionDtoExtended
                {
                    Id = a.Id,
                    Fecha = a.Fecha,
                    Total = a.DetalleAtencion.Sum(d => d.Cantidad * d.PrecioUnitario),
                    Detalles = a
                        .DetalleAtencion.Select(d => new DetalleAtencionDto
                        {
                            ProductoServicioId = d.ProductoServicioId,
                            Nombre = d.ProductoServicio != null ? d.ProductoServicio.Nombre : null,
                            Cantidad = d.Cantidad,
                            PrecioUnitario = d.PrecioUnitario,
                        })
                        .ToList(),
                    ClienteNombre = a.Cliente != null ? a.Cliente.Nombre : null,
                })
                .ToListAsync();

            dto.UltimasAtenciones = ultimas;

            return dto;
        }

        /// <summary>
        /// Cuenta la cantidad de "cortes" del barbero en el mes corriente.
        /// Filtra únicamente los registros cuya entidad ProductoServicio tenga `EsAlmacenable == true` (servicios).
        /// Devuelve total y desglose por servicio.
        /// </summary>
        public async Task<backend.Dtos.BarberoCortesMesDto> GetCortesMesAsync(
            int barberoId,
            int? year = null,
            int? month = null
        )
        {
            try
            {
                DateTime firstDay;
                DateTime nextMonth;

                if (year.HasValue && month.HasValue && month.Value >= 1 && month.Value <= 12)
                {
                    firstDay = new DateTime(year.Value, month.Value, 1);
                    nextMonth = firstDay.AddMonths(1);
                }
                else
                {
                    var now = DateTime.Now;
                    firstDay = new DateTime(now.Year, now.Month, 1);
                    nextMonth = firstDay.AddMonths(1);
                }

                var query =
                    from d in _context.DetalleAtencion
                    join a in _context.Atencion on d.AtencionId equals a.Id
                    join p in _context.ProductosServicios on d.ProductoServicioId equals p.Id
                    where
                        a.BarberoId == barberoId
                        && a.Fecha >= firstDay
                        && a.Fecha < nextMonth
                        && p.EsAlmacenable == true
                    select new
                    {
                        d.Cantidad,
                        ProductoServicioId = p.Id,
                        Nombre = p.Nombre,
                    };

                var total = await query.SumAsync(x => (int?)x.Cantidad) ?? 0;

                var porServicio = await query
                    .GroupBy(x => new { x.ProductoServicioId, x.Nombre })
                    .Select(g => new backend.Dtos.ServicioCorteDto
                    {
                        ProductoServicioId = g.Key.ProductoServicioId,
                        Nombre = g.Key.Nombre,
                        Cantidad = g.Sum(x => x.Cantidad),
                    })
                    .OrderByDescending(s => s.Cantidad)
                    .ToListAsync();

                return new backend.Dtos.BarberoCortesMesDto
                {
                    Year = firstDay.Year,
                    Month = firstDay.Month,
                    TotalCortes = total,
                    PorServicio = porServicio,
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error calculando cortes mes para barbero {BarberoId}",
                    barberoId
                );
                throw;
            }
        }
    }
}
