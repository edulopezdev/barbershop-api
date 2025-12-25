using System;
using System.Linq;
using System.Threading.Tasks;
using backend.Data;
using backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace backend.Services
{
    public class TurnoStateService : ITurnoStateService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TurnoStateService> _logger;

        public TurnoStateService(ApplicationDbContext context, ILogger<TurnoStateService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<(int caducados, int atendidos)> UpdateExpiredTurnosAsync()
        {
            var now = DateTime.Now;
            try
            {
                // Seleccionar turnos cuya FechaHora ya pasó y que estén en estado Pendiente(1) o Confirmado(2)
                var turnosQuery = _context.Turno.Where(t =>
                    t.FechaHora < now && (t.EstadoId == 1 || t.EstadoId == 2)
                );

                var turnos = await turnosQuery.ToListAsync();
                int caducados = 0;
                int atendidos = 0;

                foreach (var t in turnos)
                {
                    if (t.EstadoId == 1)
                    {
                        t.EstadoId = 4; // Caducado
                        t.ModificadoPor = "Sistema";
                        t.FechaModificacion = now;
                        t.Observacion = t.Observacion ?? "Turno caducado: no asistió.";
                        caducados++;
                    }
                    else if (t.EstadoId == 2)
                    {
                        t.EstadoId = 5; // Atendido
                        t.ModificadoPor = "Sistema";
                        t.FechaModificacion = now;
                        t.Observacion =
                            t.Observacion ?? "Turno confirmado y pasado: marcado como atendido.";
                        atendidos++;
                    }
                }

                if (caducados > 0 || atendidos > 0)
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation(
                        "Actualizados estados de turnos: {Caducados} caducados, {Atendidos} atendidos.",
                        caducados,
                        atendidos
                    );
                }

                return (caducados, atendidos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error actualizando estados de turnos expirados.");
                throw;
            }
        }
    }
}
