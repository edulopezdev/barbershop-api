using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EstadoTurnoController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public EstadoTurnoController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/estadoturno (Lista de estados)
        [HttpGet]
        public async Task<IActionResult> GetEstadosTurno()
        {
            var estadosTurno = await _context.EstadoTurno.ToListAsync();
            var totalEstados = estadosTurno.Count;

            return Ok(
                new
                {
                    status = 200,
                    message = totalEstados > 0
                        ? "Lista de estados de turno obtenida correctamente."
                        : "No hay estados de turno disponibles.",
                    totalEstados,
                    estadosTurno = estadosTurno ?? new List<EstadoTurno>(), // esto es para evitar nulls
                }
            );
        }

        // GET: api/estadoturno/{id} (Un estado específico)
        [HttpGet("{id}")]
        public async Task<IActionResult> GetEstadoTurno(int id)
        {
            var estadoTurno = await _context.EstadoTurno.FindAsync(id);
            if (estadoTurno == null)
            {
                return NotFound(
                    new
                    {
                        status = 404,
                        error = "Not Found",
                        message = "El estado de turno no existe.",
                    }
                );
            }

            return Ok(
                new
                {
                    status = 200,
                    message = "Estado de turno encontrado.",
                    estadoTurno,
                }
            );
        }

        // POST: api/estadoturno (Crear un nuevo estado)
        [HttpPost]
        public async Task<IActionResult> PostEstadoTurno(EstadoTurno estadoTurno)
        {
            if (string.IsNullOrEmpty(estadoTurno.Nombre))
            {
                return BadRequest(
                    new
                    {
                        status = 400,
                        error = "Bad Request",
                        message = "El nombre del estado de turno es obligatorio.",
                    }
                );
            }

            _context.EstadoTurno.Add(estadoTurno);
            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetEstadoTurno),
                new { id = estadoTurno.Id },
                new
                {
                    status = 201,
                    message = "Estado de turno creado correctamente.",
                    estadoTurno,
                }
            );
        }

        // PUT: api/estadoturno/{id} (Actualizar estado)
        [HttpPut("{id}")]
        public async Task<IActionResult> PutEstadoTurno(int id, EstadoTurno estadoTurno)
        {
            if (id != estadoTurno.Id)
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

            _context.Entry(estadoTurno).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.EstadoTurno.Any(e => e.Id == id))
                {
                    return NotFound(
                        new
                        {
                            status = 404,
                            error = "Not Found",
                            message = "El estado de turno no existe.",
                        }
                    );
                }
                throw;
            }

            return Ok(
                new
                {
                    status = 200,
                    message = "Estado de turno actualizado correctamente.",
                    estadoTurno,
                }
            );
        }

        // DELETE: api/estadoturno/{id} (Eliminar estado)
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEstadoTurno(int id)
        {
            var estadoTurno = await _context.EstadoTurno.FindAsync(id);
            if (estadoTurno == null)
            {
                return NotFound(
                    new
                    {
                        status = 404,
                        error = "Not Found",
                        message = "El estado de turno no existe.",
                    }
                );
            }

            //  Verificar si el estado está vinculado a turnos existentes
            var tieneDependencias = await _context.Turno.AnyAsync(t => t.EstadoId == id);
            if (tieneDependencias)
            {
                return BadRequest(
                    new
                    {
                        status = 400,
                        error = "Bad Request",
                        message = "No se puede eliminar el estado de turno porque está vinculado a turnos existentes.",
                        details = new
                        {
                            EstadoId = id,
                            Relacion = "Turno",
                            Motivo = "Restricción de clave foránea",
                        },
                    }
                );
            }

            _context.EstadoTurno.Remove(estadoTurno);
            await _context.SaveChangesAsync();

            return Ok(
                new
                {
                    status = 200,
                    message = "Estado de turno eliminado correctamente.",
                    estadoTurnoEliminadoId = id,
                    detalles = "No hay referencias activas en Turno, eliminación exitosa.",
                }
            );
        }
    }
}
