using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TurnosController : ControllerBase
    {
        private readonly ITurnoService _turnoService;

        public TurnosController(ITurnoService turnoService)
        {
            _turnoService = turnoService;
        }

        // GET: api/turnos (Lista de turnos)
        [HttpGet]
        public async Task<IActionResult> GetAllTurnosAsync()
        {
            var turnos = await _turnoService.GetAllTurnosAsync();
            return Ok(
                new
                {
                    status = 200,
                    message = "Turnos obtenidos correctamente.",
                    data = turnos,
                }
            );
        }

        // GET: api/turnos/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetTurnoById(int id)
        {
            var turno = await _turnoService.GetTurnoByIdAsync(id);
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
        public async Task<IActionResult> CreateTurno([FromBody] Turno turno)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var createdTurno = await _turnoService.CreateTurnoAsync(turno);
                return CreatedAtAction(
                    nameof(GetTurnoById),
                    new { id = createdTurno.Id },
                    new
                    {
                        status = 201,
                        message = "Turno creado correctamente.",
                        turno = createdTurno,
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
        public async Task<IActionResult> UpdateTurno(int id, [FromBody] Turno turno)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var updatedTurno = await _turnoService.UpdateTurnoAsync(id, turno);
            if (updatedTurno == null)
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
                    message = "Turno actualizado correctamente.",
                    turno = updatedTurno,
                }
            );
        }

        // DELETE: api/turnos/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTurno(int id)
        {
            var deleted = await _turnoService.DeleteTurnoAsync(id);
            if (!deleted)
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

            return NoContent();
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
                var barberoExiste = await _turnoService.BarberoExisteAsync(barberoId);
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

                // Obtener disponibilidad (YA incluye la validación de turnos ocupados)
                var horarios = await _turnoService.GetDisponibilidadAsync(
                    barberoId,
                    fechaInicio.Value,
                    fechaFin.Value
                );

                // Transformar a formato deseado
                var disponibilidad = horarios.Select(h => new
                {
                    Inicio = h.Fecha.Add(h.HoraInicio),
                    Fin = h.Fecha.Add(h.HoraFin),
                });

                return Ok(
                    new
                    {
                        status = 200,
                        message = "Disponibilidad obtenida correctamente.",
                        data = disponibilidad,
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
                var turnosOcupados = await _turnoService.GetTurnosOcupadosAsync(
                    barberoId,
                    fechaInicio.Value,
                    fechaFin.Value
                );

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
    }
}
