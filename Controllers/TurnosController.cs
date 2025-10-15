using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TurnosController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public TurnosController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/turnos (Lista de turnos)
        [HttpGet]
        public IActionResult GetTurnos(int page = 1, int pageSize = 10)
        {
            var totalTurnos = _context.Turno.Count();
            var turnos = _context
                .Turno.OrderBy(t => t.FechaHora)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Ok(
                new
                {
                    status = 200,
                    message = totalTurnos > 0
                        ? "Lista de turnos obtenida correctamente."
                        : "No hay turnos disponibles.",
                    pagination = new
                    {
                        totalPages = (int)Math.Ceiling((double)totalTurnos / pageSize),
                        currentPage = page,
                        pageSize,
                        totalTurnos,
                    },
                    turnos = turnos ?? new List<Turno>(), // esto es para evitar nulls
                }
            );
        }

        // GET: api/turnos/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetTurno(int id)
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
        public async Task<IActionResult> PostTurno(Turno turno)
        {
            // Validar datos básicos antes de crear el turno
            if (turno.FechaHora == default)
            {
                return BadRequest(
                    new
                    {
                        status = 400,
                        error = "Bad Request",
                        message = "La fecha y hora del turno son obligatorias.",
                    }
                );
            }

            _context.Turno.Add(turno);
            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetTurno),
                new { id = turno.Id },
                new
                {
                    status = 201,
                    message = "Turno creado correctamente.",
                    turno,
                }
            );
        }

        // PUT: api/turnos/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> PutTurno(int id, Turno turno)
        {
            if (id != turno.Id)
            {
                return BadRequest(
                    new
                    {
                        status = 400,
                        error = "Bad Request",
                        message = "El ID en la URL no coincide con el ID del cuerpo de la solicitud.",
                    }
                );
            }

            _context.Entry(turno).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Turno.Any(e => e.Id == id))
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
                throw;
            }

            return Ok(
                new
                {
                    status = 200,
                    message = "Turno actualizado correctamente.",
                    turno,
                }
            );
        }

        // DELETE: api/turnos/{id}
        [HttpDelete("{id}")]
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

            // Verificar si el turno está vinculado en `Atencion`
            var tieneDependencias = _context.Atencion.Any(a => a.TurnoId == id);
            if (tieneDependencias)
            {
                return BadRequest(
                    new
                    {
                        status = 400,
                        error = "Bad Request",
                        message = "No se puede eliminar el turno porque está vinculado a una atención.",
                        details = new
                        {
                            TurnoId = id,
                            Relacion = "Atencion",
                            Motivo = "Restricción de clave foránea",
                        },
                    }
                );
            }

            _context.Turno.Remove(turno);
            await _context.SaveChangesAsync();

            return Ok(
                new
                {
                    status = 200,
                    message = "Turno eliminado correctamente.",
                    turnoIdEliminado = id,
                }
            );
        }

        //-------Filtros-------//
        [HttpGet("PorFecha")]
        public IActionResult GetTurnosPorFecha(DateTime fecha, int page = 1, int pageSize = 10)
        {
            var query = _context.Turno.AsQueryable();

            // Filtrar por fecha específica
            query = query.Where(t => t.FechaHora.Date == fecha.Date);

            var totalTurnos = query.Count();
            var turnos = query
                .OrderBy(t => t.FechaHora)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Ok(
                new
                {
                    status = 200,
                    message = totalTurnos > 0
                        ? "Lista de turnos obtenida correctamente."
                        : "No hay turnos en la fecha especificada.",
                    pagination = new
                    {
                        totalPages = (int)Math.Ceiling((double)totalTurnos / pageSize),
                        currentPage = page,
                        pageSize,
                        totalTurnos,
                    },
                    turnos = turnos ?? new List<Turno>(),
                }
            );
        }

        [HttpGet("PorRango")]
        public IActionResult GetTurnosPorRango(
            DateTime fechaInicio,
            DateTime fechaFin,
            int page = 1,
            int pageSize = 10
        )
        {
            var query = _context.Turno.AsQueryable();

            // 🔹 Validar que `fechaInicio` no sea mayor que `fechaFin`
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

            // 🔹 Filtrar turnos dentro del rango de fechas
            query = query.Where(t =>
                t.FechaHora.Date >= fechaInicio.Date && t.FechaHora.Date <= fechaFin.Date
            );

            var totalTurnos = query.Count();
            var turnos = query
                .OrderBy(t => t.FechaHora)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Ok(
                new
                {
                    status = 200,
                    message = totalTurnos > 0
                        ? "Lista de turnos obtenida correctamente."
                        : "No hay turnos dentro del rango de fechas especificado.",
                    pagination = new
                    {
                        totalPages = (int)Math.Ceiling((double)totalTurnos / pageSize),
                        currentPage = page,
                        pageSize,
                        totalTurnos,
                    },
                    turnos = turnos ?? new List<Turno>(),
                }
            );
        }

        [HttpGet("PorCliente")]
        public IActionResult GetTurnoPorCliente(int clienteId, int page = 1, int pageSize = 10)
        {
            var query = _context.Turno.AsQueryable();

            // Validar que `clienteId` sea válido
            if (clienteId <= 0)
            {
                return BadRequest(
                    new
                    {
                        status = 400,
                        error = "Bad Request",
                        message = "El clienteId debe ser mayor a 0.",
                    }
                );
            }

            // Filtrar por cliente asegurando que `ClienteId` existe
            query = query.Where(t => t.ClienteId == clienteId);

            var totalTurnos = query.Count();
            var turno = query
                .OrderBy(t => t.FechaHora)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Ok(
                new
                {
                    status = 200,
                    message = totalTurnos > 0
                        ? "Lista de turno obtenida correctamente."
                        : "No hay turno para el cliente especificado.",
                    pagination = new
                    {
                        totalPages = (int)Math.Ceiling((double)totalTurnos / pageSize),
                        currentPage = page,
                        pageSize,
                        totalTurnos,
                    },
                    turno = turno ?? new List<Turno>(),
                }
            );
        }

        [HttpGet("PorEstado")]
        public IActionResult GetTurnoPorEstado(int estadoId, int page = 1, int pageSize = 10)
        {
            var query = _context.Turno.AsQueryable();

            // Validar que `estadoId` sea válido
            if (estadoId <= 0)
            {
                return BadRequest(
                    new
                    {
                        status = 400,
                        error = "Bad Request",
                        message = "El estadoId debe ser mayor a 0.",
                    }
                );
            }

            // Filtrar por estado asegurando que `EstadoId` existe
            query = query.Where(t => t.EstadoId == estadoId);

            var totalTurnos = query.Count();
            var turno = query
                .OrderBy(t => t.FechaHora)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Ok(
                new
                {
                    status = 200,
                    message = totalTurnos > 0
                        ? "Lista de turno obtenida correctamente."
                        : "No hay turno con el estado especificado.",
                    pagination = new
                    {
                        totalPages = (int)Math.Ceiling((double)totalTurnos / pageSize),
                        currentPage = page,
                        pageSize,
                        totalTurnos,
                    },
                    turno = turno ?? new List<Turno>(),
                }
            );
        }
    }
}
