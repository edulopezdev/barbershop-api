using System.Security.Claims;
using backend.Data;
using backend.Dtos;
using backend.Models;
using backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TurnosController : ControllerBase
    {
        private readonly ITurnoStateService _turnoStateService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TurnosController> _logger;
        private readonly IEmailSender _emailSender;
        private readonly IConfiguration _config;

        public TurnosController(
            ApplicationDbContext context,
            ILogger<TurnosController> logger,
            IEmailSender emailSender,
            IConfiguration config,
            ITurnoStateService turnoStateService
        )
        {
            _context = context;
            _logger = logger;
            _emailSender = emailSender;
            _config = config;
            _turnoStateService = turnoStateService;
        }

        // Helper: obtener userId desde claims
        private int? GetUserIdFromClaims()
        {
            var claim =
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value
                ?? User.FindFirst("userId")?.Value;
            if (int.TryParse(claim, out var id))
                return id;
            return null;
        }

        // Helper: obtener rol desde claims
        private string? GetRoleFromClaims()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("rol")?.Value;
        }

        // GET: api/turnos (Lista de turnos)
        // Por defecto devolvemos SÓLO turnos futuros para mantener la vista limpia.
        // Clientes pueden pasar `?includePast=true` para ver historial de turnos vencidos.
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAllTurnosAsync([FromQuery] bool includePast = false)
        {
            var userId = GetUserIdFromClaims();
            var role = GetRoleFromClaims();

            if (userId == null || string.IsNullOrEmpty(role))
                return Unauthorized("Token inválido");

            try
            {
                // Actualizar estados de turnos expirados antes de listar (no bloquear si falla)
                try
                {
                    await _turnoStateService.UpdateExpiredTurnosAsync();
                }
                catch { }

                // Administrador ve todos los turnos (sin filtrar por fecha)
                if (string.Equals(role, "Administrador", StringComparison.OrdinalIgnoreCase))
                {
                    var turnosAll = await _context
                        .Turno.Include(t => t.Cliente)
                        .Include(t => t.Barbero)
                        .Include(t => t.Estado)
                        .OrderBy(t => t.FechaHora)
                        .Select(t => new
                        {
                            t.Id,
                            t.FechaHora,
                            Cliente = t.Cliente != null
                                ? new { t.ClienteId, Nombre = t.Cliente.Nombre ?? "Desconocido" }
                                : null,
                            Barbero = t.Barbero != null
                                ? new { t.BarberoId, Nombre = t.Barbero.Nombre ?? "Desconocido" }
                                : null,
                            Estado = t.Estado != null
                                ? new { t.EstadoId, Nombre = t.Estado.Nombre ?? "Desconocido" }
                                : null,
                            CantidadAtenciones = t.Atenciones.Count,
                        })
                        .ToListAsync();

                    return Ok(
                        new
                        {
                            status = 200,
                            message = "Turnos obtenidos correctamente.",
                            data = turnosAll,
                        }
                    );
                }

                // Barbero ve solo sus turnos vigentes (por defecto). Puede pedir historial con ?includePast=true
                if (string.Equals(role, "Barbero", StringComparison.OrdinalIgnoreCase))
                {
                    var queryBarbero = _context.Turno.Where(t => t.BarberoId == userId.Value);
                    if (!includePast)
                        queryBarbero = queryBarbero.Where(t => t.FechaHora > DateTime.Now);

                    var turnosBarbero = await queryBarbero
                        .Include(t => t.Cliente)
                        .Include(t => t.Estado)
                        .OrderBy(t => t.FechaHora)
                        .Select(t => new
                        {
                            t.Id,
                            t.FechaHora,
                            Cliente = t.Cliente != null
                                ? new { t.ClienteId, Nombre = t.Cliente.Nombre ?? "Desconocido" }
                                : null,
                            Estado = t.Estado != null
                                ? new { t.EstadoId, Nombre = t.Estado.Nombre ?? "Desconocido" }
                                : null,
                            CantidadAtenciones = t.Atenciones.Count,
                        })
                        .ToListAsync();

                    return Ok(
                        new
                        {
                            status = 200,
                            message = "Turnos obtenidos correctamente.",
                            data = turnosBarbero,
                        }
                    );
                }

                // Cliente ve SÓLO turnos futuros por defecto (vista limpia). Si pasa `?includePast=true` puede ver historial.
                if (string.Equals(role, "Cliente", StringComparison.OrdinalIgnoreCase))
                {
                    var queryCliente = _context.Turno.Where(t => t.ClienteId == userId.Value);
                    if (!includePast)
                        queryCliente = queryCliente.Where(t => t.FechaHora > DateTime.Now);

                    var turnosCliente = await queryCliente
                        .Include(t => t.Barbero)
                        .Include(t => t.Estado)
                        .OrderBy(t => t.FechaHora)
                        .Select(t => new
                        {
                            t.Id,
                            t.FechaHora,
                            Barbero = t.Barbero != null
                                ? new { t.BarberoId, Nombre = t.Barbero.Nombre ?? "Desconocido" }
                                : null,
                            Estado = t.Estado != null
                                ? new { t.EstadoId, Nombre = t.Estado.Nombre ?? "Desconocido" }
                                : null,
                            CantidadAtenciones = t.Atenciones.Count,
                        })
                        .ToListAsync();

                    return Ok(
                        new
                        {
                            status = 200,
                            message = "Turnos obtenidos correctamente.",
                            data = turnosCliente,
                        }
                    );
                }

                return StatusCode(
                    403,
                    new
                    {
                        status = 403,
                        error = "Forbidden",
                        message = "Rol no autorizado para listar turnos.",
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo turnos para usuario {UserId}", userId);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        // GET: api/turnos/barbero/mis-turnos
        // Devuelve por defecto los turnos vigentes (desde ahora). Soporta filtros opcionales y paginación.
        [HttpGet("barbero/mis-turnos")]
        [Authorize]
        public async Task<IActionResult> GetMisTurnosBarberoAsync(
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] bool includePast = false,
            [FromQuery] string? estados = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50
        )
        {
            var userId = GetUserIdFromClaims();
            var role = GetRoleFromClaims();

            if (userId == null || string.IsNullOrEmpty(role))
                return Unauthorized("Token inválido");

            if (!string.Equals(role, "Barbero", StringComparison.OrdinalIgnoreCase))
                return StatusCode(
                    403,
                    new
                    {
                        status = 403,
                        error = "Forbidden",
                        message = "Solo barberos pueden acceder a este endpoint.",
                    }
                );

            // Normalizar paginación
            if (page <= 0)
                page = 1;
            if (pageSize <= 0)
                pageSize = 25;
            if (pageSize > 200)
                pageSize = 200;

            try
            {
                // Por defecto, si no se solicita historial y no se pasó 'from', usamos desde ahora
                if (!includePast && from == null)
                    from = DateTime.Now;

                var query = _context.Turno.AsQueryable();
                query = query.Where(t => t.BarberoId == userId.Value);

                if (!includePast && from != null)
                    query = query.Where(t => t.FechaHora >= from.Value);

                if (from != null && includePast)
                    query = query.Where(t => t.FechaHora >= from.Value);

                if (to != null)
                    query = query.Where(t => t.FechaHora <= to.Value);

                // Filtrar por estados (csv) si se pasó
                if (!string.IsNullOrWhiteSpace(estados))
                {
                    var estadosList = estados
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                        .Where(s => int.TryParse(s, out _))
                        .Select(int.Parse)
                        .ToList();

                    if (estadosList.Count > 0)
                        query = query.Where(t => estadosList.Contains(t.EstadoId));
                }

                // Conteo total antes de paginar
                var total = await query.CountAsync();

                // Orden: si se pidió historial (includePast=true) devolvemos más reciente primero
                if (includePast)
                    query = query.OrderByDescending(t => t.FechaHora);
                else
                    query = query.OrderBy(t => t.FechaHora);

                var items = await query
                    .Include(t => t.Cliente)
                    .Include(t => t.Estado)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(t => new
                    {
                        t.Id,
                        t.FechaHora,
                        Cliente = t.Cliente != null
                            ? new { t.ClienteId, Nombre = t.Cliente.Nombre ?? "Desconocido" }
                            : null,
                        Estado = t.Estado != null
                            ? new { t.EstadoId, Nombre = t.Estado.Nombre ?? "Desconocido" }
                            : null,
                        CantidadAtenciones = t.Atenciones.Count,
                    })
                    .ToListAsync();

                var totalPages = (int)Math.Ceiling(total / (double)pageSize);

                return Ok(
                    new
                    {
                        status = 200,
                        message = "Turnos obtenidos correctamente.",
                        data = items,
                        pagination = new
                        {
                            total,
                            page,
                            pageSize,
                            totalPages,
                        },
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo turnos del barbero {UserId}", userId);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        // GET: api/turnos/{id}
        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetTurnoById(int id)
        {
            var userId = GetUserIdFromClaims();
            var role = GetRoleFromClaims();

            if (userId == null || string.IsNullOrEmpty(role))
                return Unauthorized("Token inválido");

            var turno = await _context
                .Turno.Include(t => t.Cliente)
                .Include(t => t.Barbero)
                .Include(t => t.Estado)
                .Where(t => t.Id == id)
                .Select(t => new
                {
                    t.Id,
                    t.FechaHora,
                    Cliente = t.Cliente != null
                        ? new { t.ClienteId, Nombre = t.Cliente.Nombre ?? "Desconocido" }
                        : null,
                    Barbero = t.Barbero != null
                        ? new { t.BarberoId, Nombre = t.Barbero.Nombre ?? "Desconocido" }
                        : null,
                    Estado = t.Estado != null
                        ? new { t.EstadoId, Nombre = t.Estado.Nombre ?? "Desconocido" }
                        : null,
                    CantidadAtenciones = t.Atenciones.Count,
                })
                .FirstOrDefaultAsync();

            if (turno == null)
                return NotFound(
                    new
                    {
                        status = 404,
                        error = "Not Found",
                        message = "El turno no existe.",
                    }
                );

            // Si el turno ya fue marcado por el sistema como Caducado(4) o Atendido(5), no permitir cambios manuales
            if (turno.Estado != null && (turno.Estado.EstadoId == 4 || turno.Estado.EstadoId == 5))
            {
                return BadRequest(
                    new
                    {
                        status = 400,
                        error = "Bad Request",
                        message = "No se puede modificar el estado de un turno que ya fue marcado como Caducado o Atendido.",
                    }
                );
            }

            return Ok(
                new
                {
                    status = 200,
                    message = "Turno encontrado.",
                    turno,
                }
            );
        }

        // POST: api/turnos
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateTurno([FromBody] Turno turno)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userId = GetUserIdFromClaims();
                var role = GetRoleFromClaims();
                if (userId == null || string.IsNullOrEmpty(role))
                    return Unauthorized("Token inválido");

                // ✅ OBTENER CONFIGURACIONES DESDE BASE DE DATOS
                var configuraciones = await ObtenerConfiguracionesSistemaAsync();

                // Validación: Horario de atención cortado
                if (!IsValidBusinessHour(turno.FechaHora, configuraciones))
                {
                    return BadRequest(
                        new
                        {
                            status = 400,
                            error = "Bad Request",
                            message = $"El horario debe estar dentro de los horarios de atención: {configuraciones.HorarioMatutinoInicio}-{configuraciones.HorarioMatutinoFin} o {configuraciones.HorarioVespertinoInicio}-{configuraciones.HorarioVespertinoFin}.",
                        }
                    );
                }

                // Validación: Días laborales
                if (!IsValidBusinessDay(turno.FechaHora, configuraciones))
                {
                    return BadRequest(
                        new
                        {
                            status = 400,
                            error = "Bad Request",
                            message = "Solo se pueden reservar turnos de Lunes a Sábado.",
                        }
                    );
                }

                // Validación: Anticipación mínima
                var ahora = DateTime.Now;
                var minimaFecha = ahora.AddHours(configuraciones.AntipacionMinimaHoras);
                if (turno.FechaHora <= minimaFecha)
                {
                    return BadRequest(
                        new
                        {
                            status = 400,
                            error = "Bad Request",
                            message = $"Los turnos deben reservarse con al menos {configuraciones.AntipacionMinimaHoras} horas de anticipación.",
                        }
                    );
                }

                // Validar que el barbero existe y que es rol "Barbero" (RolId = 2)
                var barbero = await _context.Usuario.FindAsync(turno.BarberoId);
                if (barbero == null)
                    return BadRequest(
                        new
                        {
                            status = 400,
                            error = "Bad Request",
                            message = "Barbero no encontrado.",
                        }
                    );
                if (barbero.RolId != 2)
                    return BadRequest(
                        new
                        {
                            status = 400,
                            error = "Bad Request",
                            message = "El usuario seleccionado como barbero no tiene rol de Barbero.",
                        }
                    );

                // Si el solicitante es Cliente, forzamos ClienteId = userId
                if (string.Equals(role, "Cliente", StringComparison.OrdinalIgnoreCase))
                {
                    // Cliente autenticado: asignar automáticamente su Id como ClienteId
                    turno.ClienteId = userId.Value;
                }
                else
                {
                    // Si quien crea NO es Cliente (Barbero o Admin), exigir ClienteId en el body
                    if (turno.ClienteId == 0)
                    {
                        return BadRequest(
                            new
                            {
                                status = 400,
                                error = "Bad Request",
                                message = "ClienteId es requerido cuando el creador no es un cliente autenticado.",
                            }
                        );
                    }

                    // Validar que el cliente existe y que tiene rol "Cliente" (RolId = 3)
                    var cliente = await _context.Usuario.FindAsync(turno.ClienteId);
                    if (cliente == null)
                        return BadRequest(
                            new
                            {
                                status = 400,
                                error = "Bad Request",
                                message = "Cliente no encontrado.",
                            }
                        );
                    if (cliente.RolId != 3)
                        return BadRequest(
                            new
                            {
                                status = 400,
                                error = "Bad Request",
                                message = "El usuario proporcionado como cliente no tiene rol de Cliente.",
                            }
                        );
                }

                // Límite de turnos por cliente (no por día): contar solo turnos activos
                // (Pendiente = 1, Confirmado = 2). Los turnos cancelados/completados
                // no se cuentan, por lo que si el cliente cancela puede sacar otro.
                {
                    var turnosActivos = await _context
                        .Turno.Where(t =>
                            t.ClienteId == turno.ClienteId && (t.EstadoId == 1 || t.EstadoId == 2)
                        )
                        .CountAsync();

                    if (turnosActivos >= configuraciones.MaxTurnosPorClientePorDia)
                    {
                        return BadRequest(
                            new
                            {
                                status = 400,
                                error = "Bad Request",
                                message = $"No podés tener más de {configuraciones.MaxTurnosPorClientePorDia} turnos activos al mismo tiempo.",
                            }
                        );
                    }
                }

                // ✅ VALIDACIÓN: Duración de turno (desde configuración)
                var slotStart = turno.FechaHora;
                var slotEnd = slotStart.AddMinutes(configuraciones.DuracionTurnoMinutos);

                var conflict = await _context.Turno.AnyAsync(t =>
                    t.BarberoId == turno.BarberoId
                    && (
                        t.FechaHora < slotEnd
                        && t.FechaHora.AddMinutes(configuraciones.DuracionTurnoMinutos) > slotStart
                    )
                );

                if (conflict)
                {
                    return Conflict(
                        new
                        {
                            status = 409,
                            error = "Conflict",
                            message = $"El barbero ya tiene un turno en ese horario (turnos duran {configuraciones.DuracionTurnoMinutos} minutos).",
                        }
                    );
                }

                // Estado inicial: Pendiente = 1
                turno.EstadoId = 1;

                _context.Turno.Add(turno);
                await _context.SaveChangesAsync();

                return CreatedAtAction(
                    nameof(GetTurnoById),
                    new { id = turno.Id },
                    new
                    {
                        status = 201,
                        message = "Turno creado correctamente.",
                        turno,
                    }
                );
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(
                    new
                    {
                        status = 400,
                        error = "Bad Request",
                        message = ex.Message,
                    }
                );
            }
            catch (Exception ex)
            {
                return StatusCode(
                    500,
                    new
                    {
                        status = 500,
                        error = "Internal Server Error",
                        message = "Ocurrió un error inesperado: " + ex.Message,
                    }
                );
            }
        }

        // PUT: api/turnos/{id}
        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> UpdateTurno(int id, [FromBody] Turno turno)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var turnoExistente = await _context.Turno.FindAsync(id);
            if (turnoExistente == null)
            {
                return NotFound(
                    new
                    {
                        status = 404,
                        error = "Not Found",
                        message = "El turno no existe.",
                    }
                );
            }

            var userId = GetUserIdFromClaims();
            var role = GetRoleFromClaims();
            if (userId == null || string.IsNullOrEmpty(role))
                return Unauthorized("Token inválido");

            // Barbero solo puede editar sus propios turnos
            if (string.Equals(role, "Barbero", StringComparison.OrdinalIgnoreCase))
            {
                if (turnoExistente.BarberoId != userId.Value)
                    return StatusCode(
                        403,
                        new
                        {
                            status = 403,
                            error = "Forbidden",
                            message = "No tienes permiso para modificar este turno.",
                        }
                    );
            }
            // Clientes no pueden editar turnos
            if (string.Equals(role, "Cliente", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(
                    403,
                    new
                    {
                        status = 403,
                        error = "Forbidden",
                        message = "Clientes no pueden modificar turnos.",
                    }
                );
            }

            // Obtener configuración para duración de turno
            var configuraciones = await ObtenerConfiguracionesSistemaAsync();
            var duracionMinutos = configuraciones.DuracionTurnoMinutos;

            // Validar que el barbero destino exista
            var newBarberoId = turno.BarberoId;
            var newStart = turno.FechaHora;
            var newEnd = newStart.AddMinutes(duracionMinutos);

            var barberoExiste = await _context.Usuario.AnyAsync(u => u.Id == newBarberoId);
            if (!barberoExiste)
            {
                return BadRequest(
                    new
                    {
                        status = 400,
                        error = "Bad Request",
                        message = "Barbero no encontrado.",
                    }
                );
            }

            // Comprobar solapamiento: excluir el propio turno actual
            var conflict = await _context.Turno.AnyAsync(t =>
                t.Id != id
                && t.BarberoId == newBarberoId
                && (t.FechaHora < newEnd && t.FechaHora.AddMinutes(duracionMinutos) > newStart)
            );

            if (conflict)
            {
                return Conflict(
                    new
                    {
                        status = 409,
                        error = "Conflict",
                        message = $"El barbero ya tiene un turno en ese horario (turnos duran {duracionMinutos} minutos).",
                    }
                );
            }

            // Actualizar solo las propiedades reales del modelo Turno
            turnoExistente.FechaHora = turno.FechaHora;
            turnoExistente.ClienteId = turno.ClienteId;
            turnoExistente.BarberoId = turno.BarberoId;
            turnoExistente.EstadoId = turno.EstadoId;

            // Registrar auditoría de modificación
            turnoExistente.ModificadoPor = $"{role}:{userId}";
            turnoExistente.FechaModificacion = DateTime.Now;

            await _context.SaveChangesAsync();

            return Ok(
                new
                {
                    status = 200,
                    message = "Turno actualizado correctamente.",
                    turno = turnoExistente,
                }
            );
        }

        // DELETE: api/turnos/{id}
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteTurno(int id)
        {
            var turno = await _context.Turno.FindAsync(id);
            if (turno == null)
            {
                return NotFound(
                    new
                    {
                        status = 404,
                        error = "Not Found",
                        message = "El turno no existe.",
                    }
                );
            }

            var userId = GetUserIdFromClaims();
            var role = GetRoleFromClaims();
            if (userId == null || string.IsNullOrEmpty(role))
                return Unauthorized("Token inválido");

            // Administrador puede eliminar cualquier turno
            if (string.Equals(role, "Administrador", StringComparison.OrdinalIgnoreCase))
            {
                _context.Turno.Remove(turno);
                await _context.SaveChangesAsync();
                return NoContent();
            }

            // Barbero solo puede eliminar sus propios turnos
            if (string.Equals(role, "Barbero", StringComparison.OrdinalIgnoreCase))
            {
                if (turno.BarberoId != userId.Value)
                    return StatusCode(
                        403,
                        new
                        {
                            status = 403,
                            error = "Forbidden",
                            message = "No tienes permiso para eliminar este turno.",
                        }
                    );

                _context.Turno.Remove(turno);
                await _context.SaveChangesAsync();
                return NoContent();
            }

            // Clientes no pueden eliminar turnos
            return StatusCode(
                403,
                new
                {
                    status = 403,
                    error = "Forbidden",
                    message = "Clientes no pueden eliminar turnos.",
                }
            );
        }

        // GET: api/turnos/disponibilidad
        [HttpGet("disponibilidad")]
        public async Task<IActionResult> GetDisponibilidad(
            [FromQuery] int barberoId,
            [FromQuery] DateTime? fechaInicio = null,
            [FromQuery] DateTime? fechaFin = null
        )
        {
            fechaInicio ??= DateTime.Now;
            fechaFin ??= DateTime.Now.AddDays(7);

            if (fechaInicio > fechaFin)
            {
                return BadRequest(
                    new
                    {
                        status = 400,
                        error = "Bad Request",
                        message = "La fecha de inicio no puede ser mayor que la fecha de fin.",
                    }
                );
            }

            try
            {
                // Validar si el barbero existe
                var barberoExiste = await _context.Usuario.AnyAsync(u => u.Id == barberoId);
                if (!barberoExiste)
                {
                    return NotFound(
                        new
                        {
                            status = 404,
                            error = "Not Found",
                            message = "El barbero especificado no existe.",
                        }
                    );
                }

                // Obtener configuraciones (duración de turno)
                var configuraciones = await ObtenerConfiguracionesSistemaAsync();
                var duracion = configuraciones.DuracionTurnoMinutos;

                // Obtener disponibilidad (lista de turnos ocupados -> se devuelve inicio/fin usando duración configurable)
                var horarios = await _context
                    .Turno.Where(t =>
                        t.BarberoId == barberoId
                        && t.FechaHora >= fechaInicio
                        && t.FechaHora <= fechaFin
                    )
                    .Select(t => new
                    {
                        Inicio = t.FechaHora,
                        Fin = t.FechaHora.AddMinutes(duracion),
                    })
                    .ToListAsync();

                return Ok(
                    new
                    {
                        status = 200,
                        message = "Disponibilidad obtenida correctamente.",
                        data = horarios,
                    }
                );
            }
            catch (Exception ex)
            {
                return StatusCode(
                    500,
                    new
                    {
                        status = 500,
                        error = "Internal Server Error",
                        message = "Ocurrió un error inesperado: " + ex.Message,
                    }
                );
            }
        }

        // GET: api/turnos/ocupados
        [HttpGet("ocupados")]
        public async Task<IActionResult> GetTurnosOcupados(
            [FromQuery] int barberoId,
            [FromQuery] DateTime? fechaInicio = null,
            [FromQuery] DateTime? fechaFin = null
        )
        {
            fechaInicio ??= DateTime.Now;
            fechaFin ??= DateTime.Now.AddDays(7);

            if (fechaInicio > fechaFin)
            {
                return BadRequest(
                    new
                    {
                        status = 400,
                        error = "Bad Request",
                        message = "La fecha de inicio no puede ser mayor que la fecha de fin.",
                    }
                );
            }

            try
            {
                var turnosOcupados = await _context
                    .Turno.Where(t =>
                        t.BarberoId == barberoId
                        && t.FechaHora >= fechaInicio
                        && t.FechaHora <= fechaFin
                    )
                    .ToListAsync();

                return Ok(
                    new
                    {
                        status = 200,
                        message = "Turnos ocupados obtenidos correctamente.",
                        data = turnosOcupados,
                    }
                );
            }
            catch (Exception ex)
            {
                return StatusCode(
                    500,
                    new
                    {
                        status = 500,
                        error = "Internal Server Error",
                        message = "Ocurrió un error inesperado: " + ex.Message,
                    }
                );
            }
        }

        // GET: api/turnos/mis-turnos
        [HttpGet("mis-turnos")]
        [Authorize]
        public async Task<IActionResult> GetMisTurnos()
        {
            var userId = GetUserIdFromClaims();
            var rolClaim = GetRoleFromClaims();

            if (userId == null || string.IsNullOrEmpty(rolClaim))
                return Unauthorized("Token inválido");

            if (!string.Equals(rolClaim, "Cliente", StringComparison.OrdinalIgnoreCase))
                return StatusCode(
                    403,
                    new
                    {
                        status = 403,
                        error = "Forbidden",
                        message = "Solo clientes pueden acceder a este endpoint",
                    }
                );

            try
            {
                // Mostrar solo turnos futuros (después del momento actual)
                var ahora = DateTime.Now;
                var turnos = await _context
                    .Turno.Where(t => t.ClienteId == userId.Value && t.FechaHora > ahora)
                    .Include(t => t.Barbero)
                    .Include(t => t.Estado)
                    .OrderBy(t => t.FechaHora)
                    .Select(t => new
                    {
                        t.Id,
                        t.FechaHora,
                        Barbero = t.Barbero != null
                            ? new { t.BarberoId, Nombre = t.Barbero.Nombre ?? "Desconocido" }
                            : null,
                        Estado = t.Estado != null
                            ? new { t.EstadoId, Nombre = t.Estado.Nombre ?? "Desconocido" }
                            : null,
                        CantidadAtenciones = t.Atenciones.Count,
                    })
                    .ToListAsync();

                return Ok(
                    new
                    {
                        success = true,
                        turnos = turnos,
                        total = turnos.Count,
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo turnos del cliente {ClienteId}", userId);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        // GET: api/turnos/mis-turnos/{id}
        [HttpGet("mis-turnos/{id}")]
        [Authorize]
        public async Task<IActionResult> GetMiTurno(int id)
        {
            var userId = GetUserIdFromClaims();
            var rolClaim = GetRoleFromClaims();

            if (userId == null || string.IsNullOrEmpty(rolClaim))
                return Unauthorized("Token inválido");

            if (!string.Equals(rolClaim, "Cliente", StringComparison.OrdinalIgnoreCase))
                return StatusCode(
                    403,
                    new
                    {
                        status = 403,
                        error = "Forbidden",
                        message = "Solo clientes pueden acceder a este endpoint",
                    }
                );

            try
            {
                var turno = await _context
                    .Turno.Include(t => t.Atenciones)
                    .Where(t => t.Id == id && t.ClienteId == userId.Value)
                    .Select(t => new
                    {
                        t.Id,
                        t.FechaHora,
                        t.ClienteId,
                        t.BarberoId,
                        t.EstadoId,
                        Atenciones = t.Atenciones.Select(a => new { a.Id }).ToList(),
                    })
                    .FirstOrDefaultAsync();

                if (turno == null)
                    return NotFound("Turno no encontrado");

                return Ok(new { success = true, turno = turno });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error obteniendo turno {TurnoId} del cliente {ClienteId}",
                    id,
                    userId
                );
                return StatusCode(500, "Error interno del servidor");
            }
        }

        // POST: api/turnos/{id}/estado
        [HttpPost("{id}/estado")]
        [Authorize]
        public async Task<IActionResult> ChangeEstado(int id, [FromBody] ChangeEstadoDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetUserIdFromClaims();
            var role = GetRoleFromClaims();
            if (userId == null || string.IsNullOrEmpty(role))
                return Unauthorized("Token inválido");

            // Obtener turno CON relaciones para el email
            var turno = await _context
                .Turno.Include(t => t.Atenciones)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (turno == null)
                return NotFound(
                    new
                    {
                        status = 404,
                        error = "Not Found",
                        message = "El turno no existe.",
                    }
                );

            // Obtener información del cliente y barbero para el email
            var cliente = await _context.Usuario.FindAsync(turno.ClienteId);
            var barbero = await _context.Usuario.FindAsync(turno.BarberoId);

            if (cliente == null || barbero == null)
                return BadRequest(
                    new
                    {
                        status = 400,
                        error = "Bad Request",
                        message = "Cliente o barbero no encontrado.",
                    }
                );

            // Sólo permitir estados 2 (Confirmado) y 3 (Cancelado)
            if (dto.EstadoId != 2 && dto.EstadoId != 3)
                return BadRequest(
                    new
                    {
                        status = 400,
                        error = "Bad Request",
                        message = "Estado inválido.",
                    }
                );

            // Barbero sólo puede cambiar sus propios turnos
            if (string.Equals(role, "Barbero", StringComparison.OrdinalIgnoreCase))
            {
                if (turno.BarberoId != userId.Value)
                    return StatusCode(
                        403,
                        new
                        {
                            status = 403,
                            error = "Forbidden",
                            message = "No tienes permiso para modificar el estado de este turno.",
                        }
                    );
            }

            // Clientes no pueden cambiar estado
            if (string.Equals(role, "Cliente", StringComparison.OrdinalIgnoreCase))
                return StatusCode(
                    403,
                    new
                    {
                        status = 403,
                        error = "Forbidden",
                        message = "Clientes no pueden modificar el estado de turnos.",
                    }
                );

            // Administrador puede cambiar cualquiera
            turno.EstadoId = dto.EstadoId;

            // Guardar observación si se proporciona (especialmente útil para cancelaciones)
            if (!string.IsNullOrEmpty(dto.Observacion))
            {
                turno.Observacion = dto.Observacion;
            }

            // Registrar auditoría de modificación
            turno.ModificadoPor = $"{role}:{userId}";
            turno.FechaModificacion = DateTime.Now;

            await _context.SaveChangesAsync();

            // ENVIAR EMAIL AL CLIENTE según el nuevo estado
            _ = Task.Run(async () =>
            {
                try
                {
                    // Validar que el email del cliente existe
                    if (string.IsNullOrEmpty(cliente.Email))
                    {
                        _logger.LogWarning(
                            "Email no disponible para cliente {ClienteId} en turno {TurnoId}",
                            cliente.Id,
                            turno.Id
                        );
                        return;
                    }

                    // Validar que el nombre del barbero existe
                    if (string.IsNullOrEmpty(barbero.Nombre))
                    {
                        _logger.LogWarning(
                            "Nombre de barbero no disponible para turno {TurnoId}",
                            turno.Id
                        );
                        return;
                    }

                    var fechaTurno = turno.FechaHora.ToString(
                        "dddd, dd 'de' MMMM 'de' yyyy",
                        new System.Globalization.CultureInfo("es-ES")
                    );
                    var horaTurno = turno.FechaHora.ToString("HH:mm");
                    var nombreCliente = cliente.Nombre ?? "Cliente";
                    var nombreBarbero = barbero.Nombre;

                    if (dto.EstadoId == 2) // Confirmado
                    {
                        var precioEstimado = "A confirmar";

                        var htmlTemplate = await _emailSender.GetTurnoConfirmadoTemplateAsync(
                            nombreCliente,
                            fechaTurno,
                            horaTurno,
                            nombreBarbero,
                            precioEstimado
                        );

                        await _emailSender.SendEmailAsync(
                            cliente.Email,
                            "✅ Turno confirmado - Forest Barber",
                            htmlTemplate
                        );

                        _logger.LogInformation(
                            "Email de confirmación enviado a {Email} para turno {TurnoId}",
                            cliente.Email,
                            turno.Id
                        );
                    }
                    else if (dto.EstadoId == 3) // Cancelado
                    {
                        var htmlTemplate = await _emailSender.GetTurnoCanceladoTemplateAsync(
                            nombreCliente,
                            fechaTurno,
                            horaTurno,
                            nombreBarbero
                        );

                        await _emailSender.SendEmailAsync(
                            cliente.Email,
                            "❌ Turno cancelado - Forest Barber",
                            htmlTemplate
                        );

                        _logger.LogInformation(
                            "Email de cancelación enviado a {Email} para turno {TurnoId}",
                            cliente.Email,
                            turno.Id
                        );
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error enviando email de notificación de estado para turno {TurnoId}",
                        turno.Id
                    );
                }
            });

            return Ok(
                new
                {
                    status = 200,
                    message = "Estado actualizado correctamente y cliente notificado.",
                    turno,
                }
            );
        }

        // NUEVO ENDPOINT: Cliente cancela su propio turno
        // POST: api/turnos/{id}/cancelar
        [HttpPost("{id}/cancelar")]
        [Authorize]
        public async Task<IActionResult> CancelarTurno(int id, [FromBody] CancelarTurnoDto? dto)
        {
            var userId = GetUserIdFromClaims();
            var role = GetRoleFromClaims();
            if (userId == null || string.IsNullOrEmpty(role))
                return Unauthorized("Token inválido");

            if (!string.Equals(role, "Cliente", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(
                    403,
                    new
                    {
                        status = 403,
                        error = "Forbidden",
                        message = "Solo clientes pueden usar este endpoint para cancelar sus turnos.",
                    }
                );
            }

            var turno = await _context.Turno.FindAsync(id);
            if (turno == null)
            {
                return NotFound(
                    new
                    {
                        status = 404,
                        error = "Not Found",
                        message = "El turno no existe.",
                    }
                );
            }

            if (turno.ClienteId != userId.Value)
            {
                return StatusCode(
                    403,
                    new
                    {
                        status = 403,
                        error = "Forbidden",
                        message = "No podés cancelar un turno que no es tuyo.",
                    }
                );
            }

            if (turno.EstadoId == 3)
            {
                return BadRequest(
                    new
                    {
                        status = 400,
                        error = "Bad Request",
                        message = "El turno ya está cancelado.",
                    }
                );
            }

            var configuraciones = await ObtenerConfiguracionesSistemaAsync();

            // Verificar ventana de cancelación
            var limiteCancelacion = DateTime.Now.AddHours(configuraciones.CancelacionMinimaHoras);
            if (turno.FechaHora <= limiteCancelacion)
            {
                return BadRequest(
                    new
                    {
                        status = 400,
                        error = "Bad Request",
                        message = $"Los turnos solo pueden cancelarse con al menos {configuraciones.CancelacionMinimaHoras} horas de anticipación.",
                    }
                );
            }

            // Si hay observación enviada pero la configuración no lo permite
            if (
                !configuraciones.ObservacionCancelacionHabilitada
                && !string.IsNullOrWhiteSpace(dto?.Observacion)
            )
            {
                return BadRequest(
                    new
                    {
                        status = 400,
                        error = "Bad Request",
                        message = "Observación de cancelación no está habilitada en la configuración del sistema.",
                    }
                );
            }

            // Guardar observación si está habilitada
            if (configuraciones.ObservacionCancelacionHabilitada)
            {
                string? obs = dto?.Observacion?.Trim();

                if (!string.IsNullOrEmpty(obs))
                {
                    if (obs.Length > 500)
                        obs = obs.Substring(0, 500);

                    turno.Observacion = obs;
                }
                else
                {
                    turno.Observacion = null;
                }
            }

            // Realizar cancelación
            turno.EstadoId = 3;

            // Auditoría
            turno.ModificadoPor = $"{role}:{userId}";
            turno.FechaModificacion = DateTime.Now;

            await _context.SaveChangesAsync();

            // Notificación por email
            _ = Task.Run(async () =>
            {
                try
                {
                    var cliente = await _context.Usuario.FindAsync(turno.ClienteId);
                    var barbero = await _context.Usuario.FindAsync(turno.BarberoId);

                    if (cliente == null || barbero == null)
                        return;

                    if (string.IsNullOrEmpty(cliente.Email))
                    {
                        _logger.LogWarning(
                            "Email no disponible para cliente {ClienteId}",
                            cliente.Id
                        );
                    }
                    else
                    {
                        var fechaTurno = turno.FechaHora.ToString(
                            "dddd, dd 'de' MMMM 'de' yyyy",
                            new System.Globalization.CultureInfo("es-ES")
                        );
                        var horaTurno = turno.FechaHora.ToString("HH:mm");
                        var nombreCliente = cliente.Nombre ?? "Cliente";
                        var nombreBarbero = barbero.Nombre ?? "Barbero";

                        var htmlTemplate = await _emailSender.GetTurnoCanceladoTemplateAsync(
                            nombreCliente,
                            fechaTurno,
                            horaTurno,
                            nombreBarbero
                        );

                        await _emailSender.SendEmailAsync(
                            cliente.Email,
                            "❌ Turno cancelado - Forest Barber",
                            htmlTemplate
                        );

                        _logger.LogInformation(
                            "Email de cancelación enviado a {Email} para turno {TurnoId}",
                            cliente.Email,
                            turno.Id
                        );
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error enviando email de cancelación para turno {TurnoId}",
                        turno.Id
                    );
                }
            });

            return Ok(
                new
                {
                    status = 200,
                    message = "Turno cancelado correctamente.",
                    turno,
                }
            );
        }

        // GET: api/turnos/horarios-disponibles
        [HttpGet("horarios-disponibles")]
        public async Task<IActionResult> GetHorariosDisponibles(
            [FromQuery] int barberoId,
            [FromQuery] DateTime fecha
        )
        {
            try
            {
                // Validar que el barbero existe
                var barberoExiste = await _context.Usuario.AnyAsync(u => u.Id == barberoId);
                if (!barberoExiste)
                {
                    return NotFound(
                        new
                        {
                            status = 404,
                            error = "Not Found",
                            message = "El barbero especificado no existe.",
                        }
                    );
                }

                var configuraciones = await ObtenerConfiguracionesSistemaAsync();

                // Validar día laboral
                if (!IsValidBusinessDay(fecha, configuraciones))
                {
                    return Ok(
                        new
                        {
                            status = 200,
                            message = "Domingo cerrado - sin horarios disponibles.",
                            data = new List<object>(),
                        }
                    );
                }
                var duracion = configuraciones.DuracionTurnoMinutos;
                var anticipacionHoras = configuraciones.AntipacionMinimaHoras;

                // Parsear horarios desde configuración
                if (!TimeSpan.TryParse(configuraciones.HorarioMatutinoInicio, out var matInicio))
                    matInicio = new TimeSpan(10, 0, 0);
                if (!TimeSpan.TryParse(configuraciones.HorarioMatutinoFin, out var matFin))
                    matFin = new TimeSpan(13, 0, 0);
                if (!TimeSpan.TryParse(configuraciones.HorarioVespertinoInicio, out var vesInicio))
                    vesInicio = new TimeSpan(17, 0, 0);
                if (!TimeSpan.TryParse(configuraciones.HorarioVespertinoFin, out var vesFin))
                    vesFin = new TimeSpan(21, 0, 0);

                var slotsDisponibles = new List<object>();

                // Helper local para generar slots en un rango
                async Task GenerarSlotsEnRango(
                    TimeSpan periodoInicio,
                    TimeSpan periodoFin,
                    string nombreTurno
                )
                {
                    var slotStart = periodoInicio;
                    var slotEndLimit = periodoFin - TimeSpan.FromMinutes(duracion);
                    for (
                        var t = slotStart;
                        t <= slotEndLimit;
                        t = t.Add(TimeSpan.FromMinutes(duracion))
                    )
                    {
                        var fechaSlot = fecha.Date.Add(t);
                        // Respetar anticipación mínima
                        if (fechaSlot <= DateTime.Now.AddHours(anticipacionHoras))
                            continue;

                        // Comprobar ocupación (solapamiento)
                        var ocupado = await _context.Turno.AnyAsync(tt =>
                            tt.BarberoId == barberoId
                            && (
                                tt.FechaHora < fechaSlot.AddMinutes(duracion)
                                && tt.FechaHora.AddMinutes(duracion) > fechaSlot
                            )
                        );

                        if (!ocupado)
                        {
                            slotsDisponibles.Add(
                                new
                                {
                                    FechaHora = fechaSlot,
                                    Hora = fechaSlot.ToString("HH:mm"),
                                    Turno = nombreTurno,
                                    Disponible = true,
                                }
                            );
                        }
                    }
                }

                await GenerarSlotsEnRango(matInicio, matFin, "Matutino");
                await GenerarSlotsEnRango(vesInicio, vesFin, "Vespertino");

                return Ok(
                    new
                    {
                        status = 200,
                        message = "Horarios disponibles obtenidos correctamente.",
                        data = slotsDisponibles,
                        fecha = fecha.ToString("yyyy-MM-dd"),
                        totalSlots = slotsDisponibles.Count,
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error obteniendo horarios disponibles para barbero {BarberoId} en {Fecha}",
                    barberoId,
                    fecha
                );
                return StatusCode(
                    500,
                    new
                    {
                        status = 500,
                        error = "Internal Server Error",
                        message = "Error interno del servidor.",
                    }
                );
            }
        }

        // GET: api/turnos/barberos
        // Endpoint público para listar barberos (sin información sensible)
        [HttpGet("barberos")]
        public async Task<IActionResult> GetBarberos(
            [FromQuery] string? search = null,
            [FromQuery] bool onlyActive = true,
            [FromQuery] int limit = 100
        )
        {
            // Normalizar límite
            if (limit <= 0)
                limit = 100;
            if (limit > 1000)
                limit = 1000;

            try
            {
                var query = _context.Usuario.AsQueryable();

                // Filtrar por rol Barbero (RolId = 2)
                query = query.Where(u => u.RolId == 2);

                // Filtrar por activo si corresponde
                if (onlyActive)
                    query = query.Where(u => u.Activo);

                // Búsqueda por nombre parcial
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim().ToLower();
                    query = query.Where(u => u.Nombre != null && u.Nombre.ToLower().Contains(s));
                }

                var barberos = await query
                    .OrderBy(u => u.Nombre ?? "")
                    .Select(u => new
                    {
                        u.Id,
                        Nombre = u.Nombre ?? "Desconocido",
                        u.Avatar,
                        u.Telefono,
                        // Añadir esta línea para incluir AvatarUrl
                        AvatarUrl = string.IsNullOrEmpty(u.Avatar)
                            ? $"{Request.Scheme}://{Request.Host}/avatars/no_avatar.jpg"
                            : $"{Request.Scheme}://{Request.Host}{u.Avatar}",
                    })
                    .Take(limit)
                    .ToListAsync();

                return Ok(
                    new
                    {
                        status = 200,
                        message = "Barberos obtenidos correctamente.",
                        data = barberos,
                        total = barberos.Count,
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo lista de barberos");
                return StatusCode(
                    500,
                    new
                    {
                        status = 500,
                        error = "Internal Server Error",
                        message = "Error interno del servidor.",
                    }
                );
            }
        }

        // Helper: Validar días laborales (usando configuraciones)
        private bool IsValidBusinessDay(DateTime fechaHora, ConfiguracionesSistema configuraciones)
        {
            // Si existe flag explicito para domingo cerrado
            if (configuraciones.DomingoCerrado && fechaHora.DayOfWeek == DayOfWeek.Sunday)
                return false;

            // Si se definió lista de días laborales cortado (L,M,X,J,V,S), respetarla
            if (!string.IsNullOrWhiteSpace(configuraciones.DiasLaboralesCortado))
            {
                var dias = configuraciones
                    .DiasLaboralesCortado.Split(
                        ',',
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                    )
                    .Select(d => d.ToUpper())
                    .ToList();

                var map = new Dictionary<string, DayOfWeek>
                {
                    { "L", DayOfWeek.Monday },
                    { "M", DayOfWeek.Tuesday },
                    { "X", DayOfWeek.Wednesday },
                    { "J", DayOfWeek.Thursday },
                    { "V", DayOfWeek.Friday },
                    { "S", DayOfWeek.Saturday },
                    { "D", DayOfWeek.Sunday },
                };

                var valido = dias.Any(s => map.ContainsKey(s) && map[s] == fechaHora.DayOfWeek);
                return valido;
            }

            // Default: Lunes a Sábado
            return fechaHora.DayOfWeek != DayOfWeek.Sunday;
        }

        // ✅ Helper: Obtener configuraciones desde base de datos con cache en memoria
        private async Task<ConfiguracionesSistema> ObtenerConfiguracionesSistemaAsync()
        {
            var configuraciones = new ConfiguracionesSistema();

            var configs = await _context
                .ConfiguracionesSistema.Where(c =>
                    new[]
                    {
                        "DURACION_TURNO_MINUTOS",
                        "MAX_TURNOS_POR_CLIENTE",
                        "MAX_TURNOS_POR_CLIENTE_POR_DIA",
                        "ANTICIPACION_MINIMA_HORAS",
                        "HORARIO_MATUTINO_INICIO",
                        "HORARIO_MATUTINO_FIN",
                        "HORARIO_VESPERTINO_INICIO",
                        "HORARIO_VESPERTINO_FIN",
                        "ULTIMO_TURNO_MATUTINO",
                        "ULTIMO_TURNO_VESPERTINO",
                        "DIAS_LABORALES_CORTADO",
                        "DOMINGO_CERRADO",
                        "CANCELACION_MINIMA_HORAS",
                        "OBSERVACION_CANCELACION_HABILITADA",
                    }.Contains(c.Clave)
                )
                .ToDictionaryAsync(c => c.Clave, c => c.Valor);

            // Valores por defecto si no existen en BD
            configuraciones.DuracionTurnoMinutos = int.TryParse(
                configs.GetValueOrDefault("DURACION_TURNO_MINUTOS"),
                out var duracion
            )
                ? duracion
                : 60;
            configuraciones.MaxTurnosPorClientePorDia = int.TryParse(
                // Preferir la nueva clave; fallback a la antigua por compatibilidad
                configs.GetValueOrDefault("MAX_TURNOS_POR_CLIENTE")
                    ?? configs.GetValueOrDefault("MAX_TURNOS_POR_CLIENTE_POR_DIA"),
                out var maxTurnos
            )
                ? maxTurnos
                : 3;
            configuraciones.AntipacionMinimaHoras = int.TryParse(
                configs.GetValueOrDefault("ANTICIPACION_MINIMA_HORAS"),
                out var anticipacion
            )
                ? anticipacion
                : 2;

            // NUEVO: tiempo mínimo (horas) antes del turno para que un cliente pueda cancelar
            configuraciones.CancelacionMinimaHoras = int.TryParse(
                configs.GetValueOrDefault("CANCELACION_MINIMA_HORAS"),
                out var cancelMin
            )
                ? cancelMin
                : 1; // valor por defecto: 1 hora

            // NUEVO: flag para permitir que cliente deje observación al cancelar
            configuraciones.ObservacionCancelacionHabilitada = bool.TryParse(
                configs.GetValueOrDefault("OBSERVACION_CANCELACION_HABILITADA"),
                out var obsHabilitado
            )
                ? obsHabilitado
                : false;

            configuraciones.HorarioMatutinoInicio =
                configs.GetValueOrDefault("HORARIO_MATUTINO_INICIO") ?? "10:00";
            configuraciones.HorarioMatutinoFin =
                configs.GetValueOrDefault("HORARIO_MATUTINO_FIN") ?? "13:00";
            configuraciones.HorarioVespertinoInicio =
                configs.GetValueOrDefault("HORARIO_VESPERTINO_INICIO") ?? "17:00";
            configuraciones.HorarioVespertinoFin =
                configs.GetValueOrDefault("HORARIO_VESPERTINO_FIN") ?? "21:00";

            // Últimos slots permitidos (para que turno + duración quepan)
            configuraciones.UltimoTurnoMatutino =
                configs.GetValueOrDefault("ULTIMO_TURNO_MATUTINO") ?? "12:00";
            configuraciones.UltimoTurnoVespertino =
                configs.GetValueOrDefault("ULTIMO_TURNO_VESPERTINO") ?? "20:00";

            // Días laborales en formato corto (L,M,X,J,V,S) y flag domingo cerrado
            configuraciones.DiasLaboralesCortado =
                configs.GetValueOrDefault("DIAS_LABORALES_CORTADO") ?? "L,M,X,J,V,S";
            configuraciones.DomingoCerrado = bool.TryParse(
                configs.GetValueOrDefault("DOMINGO_CERRADO"),
                out var domingoCerrado
            )
                ? domingoCerrado
                : true;

            return configuraciones;
        }

        // ✅ Helper: Validar horario de atención cortado (usando configuraciones)
        private bool IsValidBusinessHour(DateTime fechaHora, ConfiguracionesSistema configuraciones)
        {
            // Validar que el turno (inicio + duración) quede dentro de una franja matutina o vespertina
            var slotStart = fechaHora.TimeOfDay;
            var slotEnd = fechaHora.AddMinutes(configuraciones.DuracionTurnoMinutos).TimeOfDay;

            // Parsear horarios desde configuración con defaults
            if (!TimeSpan.TryParse(configuraciones.HorarioMatutinoInicio, out var matutinoInicio))
                matutinoInicio = new TimeSpan(10, 0, 0);
            if (!TimeSpan.TryParse(configuraciones.HorarioMatutinoFin, out var matutinoFin))
                matutinoFin = new TimeSpan(13, 0, 0);

            if (
                !TimeSpan.TryParse(
                    configuraciones.HorarioVespertinoInicio,
                    out var vespertinoInicio
                )
            )
                vespertinoInicio = new TimeSpan(17, 0, 0);
            if (!TimeSpan.TryParse(configuraciones.HorarioVespertinoFin, out var vespertinoFin))
                vespertinoFin = new TimeSpan(21, 0, 0);

            // Últimos slots (si están configurados) representan el último horario DE INICIO permitido
            if (!TimeSpan.TryParse(configuraciones.UltimoTurnoMatutino, out var ultimoMatutino))
                ultimoMatutino = new TimeSpan(12, 0, 0);
            if (!TimeSpan.TryParse(configuraciones.UltimoTurnoVespertino, out var ultimoVespertino))
                ultimoVespertino = new TimeSpan(20, 0, 0);

            // Chequear matutino: inicio dentro de matutino, inicio <= ultimoMatutino y fin <= matutinoFin
            var enMatutino =
                slotStart >= matutinoInicio
                && slotStart <= ultimoMatutino
                && slotEnd <= matutinoFin;

            // Chequear vespertino: inicio dentro de vespertino, inicio <= ultimoVespertino y fin <= vespertinoFin
            var enVespertino =
                slotStart >= vespertinoInicio
                && slotStart <= ultimoVespertino
                && slotEnd <= vespertinoFin;

            return enMatutino || enVespertino;
        }
    }

    // ✅ Clase helper para configuraciones del sistema
    public class ConfiguracionesSistema
    {
        public int DuracionTurnoMinutos { get; set; } = 60;
        public int MaxTurnosPorClientePorDia { get; set; } = 3;
        public int AntipacionMinimaHoras { get; set; } = 2;

        // NUEVA propiedad: horas mínimas antes del turno para permitir cancelación por parte del cliente
        public int CancelacionMinimaHoras { get; set; } = 1;
        public string HorarioMatutinoInicio { get; set; } = "10:00";
        public string HorarioMatutinoFin { get; set; } = "13:00";
        public string HorarioVespertinoInicio { get; set; } = "17:00";
        public string HorarioVespertinoFin { get; set; } = "21:00";

        // Último turno que se puede asignar en cada bloque (para que el turno termine antes)
        public string UltimoTurnoMatutino { get; set; } = "12:00";
        public string UltimoTurnoVespertino { get; set; } = "20:00";

        // Días laborales configurados (ej: "L,M,X,J,V,S") y flag para domingo cerrado
        public string DiasLaboralesCortado { get; set; } = "L,M,X,J,V,S";
        public bool DomingoCerrado { get; set; } = true;

        // NUEVA propiedad: habilitar observación en cancelación
        public bool ObservacionCancelacionHabilitada { get; set; } = false;
    }

    // DTO pequeño para aceptar observación al cancelar (opcional)
    public class CancelarTurnoDto
    {
        public string? Observacion { get; set; }
    }
}
