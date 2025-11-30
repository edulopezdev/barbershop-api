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
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TurnosController> _logger;
        private readonly IEmailSender _emailSender;
        private readonly IConfiguration _config;

        public TurnosController(
            ApplicationDbContext context,
            ILogger<TurnosController> logger,
            IEmailSender emailSender,
            IConfiguration config
        )
        {
            _context = context;
            _logger = logger;
            _emailSender = emailSender;
            _config = config;
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
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAllTurnosAsync()
        {
            var userId = GetUserIdFromClaims();
            var role = GetRoleFromClaims();

            if (userId == null || string.IsNullOrEmpty(role))
                return Unauthorized("Token inválido");

            try
            {
                // Administrador ve todos
                if (string.Equals(role, "Administrador", StringComparison.OrdinalIgnoreCase))
                {
                    var turnosAll = await _context.Turno.ToListAsync();
                    return Ok(
                        new
                        {
                            status = 200,
                            message = "Turnos obtenidos correctamente.",
                            data = turnosAll,
                        }
                    );
                }

                // Barbero ve solo sus turnos
                if (string.Equals(role, "Barbero", StringComparison.OrdinalIgnoreCase))
                {
                    var turnosBarbero = await _context
                        .Turno.Where(t => t.BarberoId == userId.Value)
                        .OrderByDescending(t => t.FechaHora)
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

                // Cliente ve solo sus turnos
                if (string.Equals(role, "Cliente", StringComparison.OrdinalIgnoreCase))
                {
                    var turnosCliente = await _context
                        .Turno.Where(t => t.ClienteId == userId.Value)
                        .OrderByDescending(t => t.FechaHora)
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

                return Forbid("Rol no autorizado para listar turnos.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo turnos para usuario {UserId}", userId);
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

            var turno = await _context.Turno.FindAsync(id);
            if (turno == null)
                return NotFound(
                    new
                    {
                        status = 404,
                        error = "Not Found",
                        message = "El turno no existe.",
                    }
                );

            // Admin puede ver cualquiera
            if (string.Equals(role, "Administrador", StringComparison.OrdinalIgnoreCase))
                return Ok(
                    new
                    {
                        status = 200,
                        message = "Turno encontrado.",
                        turno,
                    }
                );

            // Barbero solo si es suyo
            if (string.Equals(role, "Barbero", StringComparison.OrdinalIgnoreCase))
            {
                if (turno.BarberoId == userId.Value)
                    return Ok(
                        new
                        {
                            status = 200,
                            message = "Turno encontrado.",
                            turno,
                        }
                    );
                return Forbid("No tienes permiso para ver este turno.");
            }

            // Cliente solo si es suyo
            if (string.Equals(role, "Cliente", StringComparison.OrdinalIgnoreCase))
            {
                if (turno.ClienteId == userId.Value)
                    return Ok(
                        new
                        {
                            status = 200,
                            message = "Turno encontrado.",
                            turno,
                        }
                    );
                return Forbid("No tienes permiso para ver este turno.");
            }

            return Forbid("Rol no autorizado.");
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
                if (!IsValidBusinessDay(turno.FechaHora))
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

                // Límite de turnos por cliente por día (aplicable siempre, para evitar eludir la regla)
                {
                    var fechaTurno = turno.FechaHora.Date;
                    var turnosDelDia = await _context
                        .Turno.Where(t =>
                            t.ClienteId == turno.ClienteId && t.FechaHora.Date == fechaTurno
                        )
                        .CountAsync();

                    if (turnosDelDia >= configuraciones.MaxTurnosPorClientePorDia)
                    {
                        return BadRequest(
                            new
                            {
                                status = 400,
                                error = "Bad Request",
                                message = $"No podés tener más de {configuraciones.MaxTurnosPorClientePorDia} turnos por día.",
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
                    return Forbid("No tienes permiso para modificar este turno.");
            }
            // Clientes no pueden editar turnos
            if (string.Equals(role, "Cliente", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid("Clientes no pueden modificar turnos.");
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
                    return Forbid("No tienes permiso para eliminar este turno.");

                _context.Turno.Remove(turno);
                await _context.SaveChangesAsync();
                return NoContent();
            }

            // Clientes no pueden eliminar turnos
            return Forbid("Clientes no pueden eliminar turnos.");
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
                return Forbid("Solo clientes pueden acceder a este endpoint");

            try
            {
                var turnos = await _context
                    .Turno.Include(t => t.Atenciones)
                    .Where(t => t.ClienteId == userId.Value)
                    .OrderByDescending(t => t.FechaHora)
                    .Select(t => new
                    {
                        t.Id,
                        t.FechaHora,
                        t.ClienteId,
                        t.BarberoId,
                        t.EstadoId,
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
                return Forbid("Solo clientes pueden acceder a este endpoint");

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
                    return Forbid("No tienes permiso para modificar el estado de este turno.");
            }

            // Clientes no pueden cambiar estado
            if (string.Equals(role, "Cliente", StringComparison.OrdinalIgnoreCase))
                return Forbid("Clientes no pueden modificar el estado de turnos.");

            // Administrador puede cambiar cualquiera
            turno.EstadoId = dto.EstadoId;

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

                // Validar día laboral
                if (!IsValidBusinessDay(fecha))
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

                var configuraciones = await ObtenerConfiguracionesSistemaAsync();
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
                    query = query.Where(u => u.Nombre.ToLower().Contains(s));
                }

                var barberos = await query
                    .OrderBy(u => u.Nombre)
                    .Select(u => new
                    {
                        u.Id,
                        u.Nombre,
                        u.Avatar,
                        u.Telefono,
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

        // Helper: Validar horario de atención cortado
        private bool IsValidBusinessHour(DateTime fechaHora)
        {
            var hora = fechaHora.TimeOfDay;

            // Horario matutino: 10:00 - 13:00 (último turno 12:00)
            var matinoInicio = new TimeSpan(10, 0, 0);
            var matinoFin = new TimeSpan(12, 0, 0);

            // Horario vespertino: 17:00 - 21:00 (último turno 20:00)
            var vespertinoInicio = new TimeSpan(17, 0, 0);
            var vespertinoFin = new TimeSpan(20, 0, 0);

            return (hora >= matinoInicio && hora <= matinoFin)
                || (hora >= vespertinoInicio && hora <= vespertinoFin);
        }

        // Helper: Validar días laborales (Lunes a Sábado)
        private bool IsValidBusinessDay(DateTime fechaHora)
        {
            var diaSemana = fechaHora.DayOfWeek;
            return diaSemana != DayOfWeek.Sunday; // Domingo cerrado
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
                        "MAX_TURNOS_POR_CLIENTE_POR_DIA",
                        "ANTICIPACION_MINIMA_HORAS",
                        "HORARIO_MATUTINO_INICIO",
                        "HORARIO_MATUTINO_FIN",
                        "HORARIO_VESPERTINO_INICIO",
                        "HORARIO_VESPERTINO_FIN",
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
                configs.GetValueOrDefault("MAX_TURNOS_POR_CLIENTE_POR_DIA"),
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
            configuraciones.HorarioMatutinoInicio =
                configs.GetValueOrDefault("HORARIO_MATUTINO_INICIO") ?? "10:00";
            configuraciones.HorarioMatutinoFin =
                configs.GetValueOrDefault("HORARIO_MATUTINO_FIN") ?? "13:00";
            configuraciones.HorarioVespertinoInicio =
                configs.GetValueOrDefault("HORARIO_VESPERTINO_INICIO") ?? "17:00";
            configuraciones.HorarioVespertinoFin =
                configs.GetValueOrDefault("HORARIO_VESPERTINO_FIN") ?? "21:00";

            return configuraciones;
        }

        // ✅ Helper: Validar horario de atención cortado (usando configuraciones)
        private bool IsValidBusinessHour(DateTime fechaHora, ConfiguracionesSistema configuraciones)
        {
            var hora = fechaHora.TimeOfDay;

            // Parsear horarios desde configuración
            if (!TimeSpan.TryParse(configuraciones.HorarioMatutinoInicio, out var matinoInicio))
                matinoInicio = new TimeSpan(10, 0, 0);

            if (!TimeSpan.TryParse(configuraciones.HorarioMatutinoFin, out var matinoFin))
                matinoFin = new TimeSpan(12, 0, 0);

            if (
                !TimeSpan.TryParse(
                    configuraciones.HorarioVespertinoInicio,
                    out var vespertinoInicio
                )
            )
                vespertinoInicio = new TimeSpan(17, 0, 0);

            if (!TimeSpan.TryParse(configuraciones.HorarioVespertinoFin, out var vespertinoFin))
                vespertinoFin = new TimeSpan(20, 0, 0);

            return (hora >= matinoInicio && hora <= matinoFin)
                || (hora >= vespertinoInicio && hora <= vespertinoFin);
        }
    }

    // ✅ Clase helper para configuraciones del sistema
    public class ConfiguracionesSistema
    {
        public int DuracionTurnoMinutos { get; set; } = 60;
        public int MaxTurnosPorClientePorDia { get; set; } = 3;
        public int AntipacionMinimaHoras { get; set; } = 2;
        public string HorarioMatutinoInicio { get; set; } = "10:00";
        public string HorarioMatutinoFin { get; set; } = "13:00";
        public string HorarioVespertinoInicio { get; set; } = "17:00";
        public string HorarioVespertinoFin { get; set; } = "21:00";
    }
}
