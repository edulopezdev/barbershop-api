using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Services
{
    public class TurnoService : ITurnoService
    {
        private readonly ApplicationDbContext _context;

        public TurnoService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Turno>> GetAllTurnosAsync()
        {
            return await _context.Turno.Include(t => t.Atenciones).ToListAsync();
        }

        public async Task<Turno?> GetTurnoByIdAsync(int id)
        {
            return await _context
                .Turno.Include(t => t.Atenciones)
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<Turno> CreateTurnoAsync(Turno turno)
        {
            try
            {
                // Validar que el barbero tenga disponibilidad activa
                var disponibilidad = await _context
                    .DisponibilidadBarbero.Where(d =>
                        d.BarberoId == turno.BarberoId
                        && d.Activo
                        && d.DiaSemana == (int)turno.FechaHora.DayOfWeek
                    )
                    .FirstOrDefaultAsync();

                if (disponibilidad == null)
                {
                    throw new InvalidOperationException(
                        "No hay disponibilidad configurada para el barbero en el día solicitado."
                    );
                }

                if (
                    turno.FechaHora.TimeOfDay < disponibilidad.HoraInicio
                    || turno.FechaHora.TimeOfDay >= disponibilidad.HoraFin
                )
                {
                    Console.WriteLine($"Turno solicitado: {turno.FechaHora.TimeOfDay}");
                    Console.WriteLine(
                        $"Disponibilidad: {disponibilidad.HoraInicio} - {disponibilidad.HoraFin}"
                    );

                    throw new InvalidOperationException(
                        "El barbero no tiene disponibilidad activa para el horario solicitado."
                    );
                }

                // Validar que no haya bloqueos en el rango horario
                var hayBloqueo = await _context.BloqueosHorario.AnyAsync(b =>
                    b.BarberoId == turno.BarberoId
                    && turno.FechaHora >= b.FechaHoraInicio
                    && turno.FechaHora < b.FechaHoraFin
                );

                if (hayBloqueo)
                {
                    throw new InvalidOperationException(
                        "El barbero tiene un bloqueo en el horario solicitado."
                    );
                }

                // Validar que no exista otro turno confirmado o pendiente en el mismo horario
                var hayConflicto = await _context.Turno.AnyAsync(t =>
                    t.BarberoId == turno.BarberoId
                    && t.FechaHora == turno.FechaHora
                    && (t.EstadoId == 1 || t.EstadoId == 2)
                );

                if (hayConflicto)
                {
                    throw new InvalidOperationException(
                        "Ya existe un turno confirmado o pendiente en el horario solicitado."
                    );
                }

                // Validar que la fecha no supere el límite de anticipación
                var configuracion = await _context.ConfiguracionTurno.FirstOrDefaultAsync();
                if (
                    configuracion != null
                    && (turno.FechaHora - DateTime.Now).TotalDays
                        > configuracion.DiasAnticipacionMaxima
                )
                {
                    throw new InvalidOperationException(
                        "La fecha del turno supera el límite de anticipación permitido."
                    );
                }

                // Crear el turno si todas las validaciones pasan
                await _context.Turno.AddAsync(turno);
                await _context.SaveChangesAsync();
                return turno;
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidOperationException("Error de validación: " + ex.Message, ex);
            }
            catch (Exception ex)
            {
                throw new Exception("Ocurrió un error al crear el turno.", ex);
            }
        }

        public async Task<Turno?> UpdateTurnoAsync(int id, Turno updatedTurno)
        {
            var existingTurno = await _context.Turno.FindAsync(id);
            if (existingTurno == null)
            {
                return null;
            }

            existingTurno.FechaHora = updatedTurno.FechaHora;
            existingTurno.ClienteId = updatedTurno.ClienteId;
            existingTurno.BarberoId = updatedTurno.BarberoId;
            existingTurno.EstadoId = updatedTurno.EstadoId;

            await _context.SaveChangesAsync();
            return existingTurno;
        }

        public async Task<bool> DeleteTurnoAsync(int id)
        {
            var turno = await _context.Turno.FindAsync(id);
            if (turno == null)
            {
                return false;
            }

            _context.Turno.Remove(turno);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> BarberoExisteAsync(int barberoId)
        {
            return await _context.Usuario.AnyAsync(u => u.Id == barberoId);
        }

        public async Task<IEnumerable<Horario>> GetDisponibilidadAsync(
            int barberoId,
            DateTime fechaInicio,
            DateTime fechaFin
        )
        {
            // Validar límite de anticipación
            var configuracion = await _context.ConfiguracionTurno.FirstOrDefaultAsync();
            if (configuracion == null)
            {
                throw new InvalidOperationException("No se encontró la configuración de turnos.");
            }

            if ((fechaFin - DateTime.Now).TotalDays > configuracion.DiasAnticipacionMaxima)
            {
                throw new InvalidOperationException(
                    "El rango solicitado excede el límite de anticipación permitido."
                );
            }

            // Obtener disponibilidad del barbero
            var disponibilidad = await _context
                .DisponibilidadBarbero.Where(d => d.BarberoId == barberoId && d.Activo)
                .ToListAsync();

            if (!disponibilidad.Any())
            {
                return Enumerable.Empty<Horario>();
            }

            // Ajustar fechaFin al final del día (23:59:59)
            var fechaFinAjustada = fechaFin.Date.AddDays(1).AddSeconds(-1);

            // Obtener bloqueos
            var bloqueos = await _context
                .BloqueosHorario.Where(b =>
                    b.BarberoId == barberoId
                    && b.FechaHoraInicio <= fechaFinAjustada
                    && b.FechaHoraFin >= fechaInicio
                )
                .ToListAsync();

            // Obtener turnos ocupados (solo las fechas/horas)
            var turnos = await _context
                .Turno.Where(t =>
                    t.BarberoId == barberoId
                    && t.FechaHora >= fechaInicio
                    && t.FechaHora <= fechaFinAjustada
                    && (t.EstadoId == 1 || t.EstadoId == 2)
                )
                .Select(t => t.FechaHora)
                .ToListAsync();

            // DEBUG: Ver qué turnos se encontraron
            Console.WriteLine($"=== DEBUG TURNOS OCUPADOS ===");
            Console.WriteLine($"Barbero ID: {barberoId}");
            Console.WriteLine($"Fecha Inicio: {fechaInicio}");
            Console.WriteLine($"Fecha Fin Ajustada: {fechaFinAjustada}");
            Console.WriteLine($"Turnos encontrados: {turnos.Count}");
            foreach (var t in turnos)
            {
                Console.WriteLine($"  - Turno: {t:yyyy-MM-dd HH:mm:ss.fff}");
            }

            // Crear HashSet para búsqueda rápida
            var turnosOcupadosSet = new HashSet<DateTime>(turnos);

            // Generar franjas horarias disponibles
            var franjasDisponibles = new List<Horario>();
            foreach (var dia in Enumerable.Range(0, (fechaFin - fechaInicio).Days + 1))
            {
                var fecha = fechaInicio.Date.AddDays(dia);
                var horariosDia = disponibilidad.Where(d => d.DiaSemana == (int)fecha.DayOfWeek);

                foreach (var horarioDia in horariosDia)
                {
                    var inicio = fecha.Add(horarioDia.HoraInicio);
                    var fin = fecha.Add(horarioDia.HoraFin);

                    while (inicio.AddMinutes(configuracion.DuracionSlotMinutos) <= fin)
                    {
                        var slotFin = inicio.AddMinutes(configuracion.DuracionSlotMinutos);

                        // DEBUG: Ver cada slot generado
                        Console.WriteLine($"  Slot generado: {inicio:yyyy-MM-dd HH:mm:ss.fff}");
                        Console.WriteLine(
                            $"  ¿Está en HashSet? {turnosOcupadosSet.Contains(inicio)}"
                        );

                        // Verificar si este slot está ocupado
                        var hayConflicto =
                            // Turno en este horario exacto
                            turnosOcupadosSet.Contains(inicio)
                            ||
                            // Bloqueos que interfieren
                            bloqueos.Any(b =>
                                inicio < b.FechaHoraFin && slotFin > b.FechaHoraInicio
                            );

                        Console.WriteLine($"  Hay conflicto: {hayConflicto}");

                        if (!hayConflicto)
                        {
                            franjasDisponibles.Add(
                                new Horario
                                {
                                    Fecha = fecha,
                                    HoraInicio = inicio.TimeOfDay,
                                    HoraFin = slotFin.TimeOfDay,
                                }
                            );
                        }

                        inicio = slotFin;
                    }
                }
            }

            return franjasDisponibles;
        }

        public async Task<IEnumerable<object>> GetTurnosOcupadosAsync(
            int barberoId,
            DateTime fechaInicio,
            DateTime fechaFin
        )
        {
            // Ajustar fechaFin al final del día
            var fechaFinAjustada = fechaFin.Date.AddDays(1).AddSeconds(-1);

            return await _context
                .Turno.Where(t =>
                    t.BarberoId == barberoId
                    && t.FechaHora >= fechaInicio
                    && t.FechaHora <= fechaFinAjustada
                    && (t.EstadoId == 1 || t.EstadoId == 2)
                )
                .Select(t => new
                {
                    t.Id,
                    t.FechaHora,
                    Estado = t.EstadoId == 1 ? "Pendiente" : "Confirmado",
                })
                .ToListAsync();
        }
    }
}
