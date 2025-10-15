using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RolController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public RolController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/rol (Lista de roles)
        [HttpGet]
        public async Task<IActionResult> GetRoles()
        {
            var roles = await _context.Rol.ToListAsync();

            return Ok(
                new
                {
                    status = 200,
                    message = "Lista de roles obtenida correctamente.",
                    totalRoles = roles.Count,
                    roles,
                }
            );
        }

        // GET: api/rol/{id} (Un rol específico)
        [HttpGet("{id}")]
        public async Task<IActionResult> GetRol(int id)
        {
            var rol = await _context.Rol.FindAsync(id);
            if (rol == null)
            {
                return NotFound(
                    new
                    {
                        status = 404,
                        error = "Not Found",
                        message = "El rol no existe.",
                    }
                );
            }

            return Ok(
                new
                {
                    status = 200,
                    message = "Rol encontrado.",
                    rol,
                }
            );
        }

        // POST: api/rol (Crear un nuevo rol)
        [HttpPost]
        public async Task<IActionResult> PostRol(Rol rol)
        {
            // 🔹 Validar que el nombre de rol no esté vacío
            if (string.IsNullOrEmpty(rol.NombreRol))
            {
                return BadRequest(
                    new
                    {
                        status = 400,
                        error = "Bad Request",
                        message = "El nombre del rol es obligatorio.",
                    }
                );
            }

            _context.Rol.Add(rol);
            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetRol),
                new { id = rol.Id },
                new
                {
                    status = 201,
                    message = "Rol creado correctamente.",
                    rol,
                }
            );
        }

        // PUT: api/rol/{id} (Actualizar rol)
        [HttpPut("{id}")]
        public async Task<IActionResult> PutRol(int id, Rol rol)
        {
            if (id != rol.Id)
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

            _context.Entry(rol).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Rol.Any(e => e.Id == id))
                {
                    return NotFound(
                        new
                        {
                            status = 404,
                            error = "Not Found",
                            message = "El rol no existe.",
                        }
                    );
                }
                throw;
            }

            return Ok(
                new
                {
                    status = 200,
                    message = "Rol actualizado correctamente.",
                    rol,
                }
            );
        }

        // DELETE: api/rol/{id} (Eliminar rol)
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRol(int id)
        {
            var rol = await _context.Rol.FindAsync(id);
            if (rol == null)
            {
                return NotFound(
                    new
                    {
                        status = 404,
                        error = "Not Found",
                        message = "El rol no existe.",
                    }
                );
            }

            // Verificar si el rol está asignado a usuarios
            var tieneUsuarios = _context.Usuario.Any(u => u.RolId == id);
            if (tieneUsuarios)
            {
                return BadRequest(
                    new
                    {
                        status = 400,
                        error = "Bad Request",
                        message = "No se puede eliminar el rol porque está asignado a usuarios.",
                        details = new
                        {
                            RolId = id,
                            Relacion = "Usuario",
                            Motivo = "Restricción de clave foránea",
                        },
                    }
                );
            }

            _context.Rol.Remove(rol);
            await _context.SaveChangesAsync();

            return Ok(
                new
                {
                    status = 200,
                    message = "Rol eliminado correctamente.",
                    rolEliminadoId = id,
                }
            );
        }
    }
}
