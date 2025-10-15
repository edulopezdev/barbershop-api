using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ImagenController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ImagenController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/imagen (Lista de imágenes)
        [HttpGet]
        public async Task<IActionResult> GetImagenes()
        {
            var imagenes = await _context.Imagen.ToListAsync();
            var totalImagenes = imagenes.Count;

            return Ok(
                new
                {
                    status = 200,
                    message = totalImagenes > 0
                        ? "Lista de imágenes obtenida correctamente."
                        : "No hay imágenes disponibles.",
                    totalImagenes,
                    imagenes = imagenes ?? new List<Imagen>(), // esto es para evitar nulls
                }
            );
        }

        // GET: api/imagen/{idImagen} (Una imagen específica)
        [HttpGet("{idImagen}")]
        public async Task<IActionResult> GetImagen(int idImagen)
        {
            var imagen = await _context.Imagen.FindAsync(idImagen);
            if (imagen == null)
            {
                return NotFound(
                    new
                    {
                        status = 404,
                        error = "Not Found",
                        message = "La imagen no existe.",
                    }
                );
            }

            return Ok(
                new
                {
                    status = 200,
                    message = "Imagen encontrada.",
                    imagen,
                }
            );
        }

        // POST: api/imagen (Subir nueva imagen)
        [HttpPost]
        public async Task<IActionResult> PostImagen(Imagen imagen)
        {
            if (string.IsNullOrEmpty(imagen.Ruta))
            {
                return BadRequest(
                    new
                    {
                        status = 400,
                        error = "Bad Request",
                        message = "La ruta de la imagen es obligatoria.",
                    }
                );
            }

            imagen.IdImagen = 0; // Aseguramos que la DB genere el ID automáticamente
            _context.Imagen.Add(imagen);
            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetImagen),
                new { idImagen = imagen.IdImagen },
                new
                {
                    status = 201,
                    message = "Imagen subida correctamente.",
                    imagen,
                }
            );
        }

        // PUT: api/imagen/{idImagen} (Actualizar imagen)
        [HttpPut("{idImagen}")]
        public async Task<IActionResult> PutImagen(int idImagen, Imagen imagen)
        {
            if (idImagen != imagen.IdImagen)
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

            _context.Entry(imagen).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Imagen.Any(e => e.IdImagen == idImagen))
                {
                    return NotFound(
                        new
                        {
                            status = 404,
                            error = "Not Found",
                            message = "La imagen no existe.",
                        }
                    );
                }
                throw;
            }

            return Ok(
                new
                {
                    status = 200,
                    message = "Imagen actualizada correctamente.",
                    imagen,
                }
            );
        }

        // DELETE: api/imagen/{idImagen} (Eliminar imagen)
        [HttpDelete("{idImagen}")]
        public async Task<IActionResult> DeleteImagen(int idImagen)
        {
            var existeImagen = await _context.Imagen.AnyAsync(i => i.IdImagen == idImagen);
            if (!existeImagen)
            {
                return NotFound(
                    new
                    {
                        status = 404,
                        error = "Not Found",
                        message = "La imagen no existe.",
                    }
                );
            }

            var imagen = await _context.Imagen.FindAsync(idImagen);
            if (imagen == null)
            {
                return NotFound(new { status = 404, message = "Imagen no encontrada." });
            }
            _context.Imagen.Remove(imagen);
            await _context.SaveChangesAsync();

            return Ok(
                new
                {
                    status = 200,
                    message = "Imagen eliminada correctamente.",
                    imagenEliminadaId = idImagen,
                    detalles = "No hay referencias activas, eliminación exitosa.",
                }
            );
        }
    }
}
