using backend.Data;
using backend.Dtos;
using backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductosServiciosController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ProductosServiciosController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/productosservicios (Obtener todos los productos)
        [HttpGet("almacenables")] // get de productos almacenables
        [Authorize(Roles = "Administrador,Barbero")] // perfiles autorizados: Administrador, Barbero
        public IActionResult GetProductosAlmacenables( // método para obtener los productos almacenables
            int page = 1, // aca indicamos que la paginación comienza en la página 1
            int pageSize = 10, // aca indicamos que se muestran 10 productos por página
            string? sort = null, // aca permitimos elegir el campo por el que se ordena
            string? order = null, // aca permitimos elegir el orden ascendente o descendente
            string? nombre = null, // este filtro permite buscar productos por su nombre
            string? descripcion = null, // este filtro permite buscar productos por su descripción
            decimal? precio = null, // este filtro permite buscar productos por su precio
            int? cantidad = null // este filtro permite buscar productos por su cantidad
        )
        {
            // creamos una consulta inicial para obtener los productos almacenables que esten activos
            var query = _context.ProductosServicios.Where(p => p.Activo && p.EsAlmacenable);

            // Filtros seguros
            if (!string.IsNullOrEmpty(nombre)) // verificamos si el nombre no está vacío
            {
                query = query.Where(p =>
                    !string.IsNullOrEmpty(p.Nombre) && p.Nombre.Contains(nombre)
                ); // si el nombre no está vacío, se agrega a la consulta
            }

            if (!string.IsNullOrEmpty(descripcion)) // verificamos si la descripción no está vacía
            {
                query = query.Where(p =>
                    !string.IsNullOrEmpty(p.Descripcion) && p.Descripcion.Contains(descripcion)
                ); // si la descripción no está vacía, se agrega a la consulta
            }

            if (precio.HasValue && precio.Value > 0) // verificamos si el precio es válido y mayor que cero
            {
                query = query.Where(p => p.Precio == precio.Value); // si el precio es válido y mayor que cero, se agrega a la consulta
            }

            if (cantidad.HasValue && cantidad.Value >= 0) // verificamos si la cantidad es válida y mayor o igual a cero
            {
                query = query.Where(p => p.Cantidad == cantidad.Value); // si la cantidad es válida y mayor o igual a cero, se agrega a la consulta
            }

            // Ordenamiento seguro
            switch (sort?.ToLower()) // usamos switch para elegir el campo por el que se ordena, con ToLower() hacemos que sea insensible a mayúsculas y minúsculas
            {
                case "nombre": // si el campo por el que se ordena es "nombre"
                    query =
                        order == "desc"
                            ? query.OrderByDescending(p => p.Nombre) // si el orden es descendente, se ordena de manera descendente
                            : query.OrderBy(p => p.Nombre); // si el orden es ascendente, se ordena de manera ascendente
                    break; // se sale del switch

                case "precio":
                    query =
                        order == "desc"
                            ? query.OrderByDescending(p => p.Precio)
                            : query.OrderBy(p => p.Precio);
                    break;

                case "cantidad":
                    query =
                        order == "desc"
                            ? query.OrderByDescending(p => p.Cantidad)
                            : query.OrderBy(p => p.Cantidad);
                    break;

                default:
                    query =
                        order == "desc"
                            ? query.OrderByDescending(p => p.Nombre)
                            : query.OrderBy(p => p.Nombre);
                    break;
            }

            var totalProductos = query.Count(); // aca contamos cuantos productos hay post aplicación de filtros
            var productosDb = query.Skip((page - 1) * pageSize).Take(pageSize).ToList(); // aca obtenemos los productos con la paginación

            //aca vamos a convertir los productos en DTOs con la imagen de cada producto si existe
            var productosDto = productosDb
                .Select(p =>
                {
                    var imagen = _context.Imagen.FirstOrDefault(i => // aca obtenemos la imagen de cada producto
                        i.TipoImagen == "ProductoServicio" // verificamos que el tipo de imagen sea "ProductoServicio"
                        && i.IdRelacionado == p.Id // verificamos que el id de la imagen sea igual al id del producto
                        && i.Activo == true // verificamos que la imagen esté activa
                    );

                    // aca creamos el DTO q contiene el id, nombre, descripción, precio, es almacenable, cantidad y la ruta de la imagen
                    return new ProductoServicioConImagenDto
                    {
                        Id = p.Id,
                        Nombre = p.Nombre,
                        Descripcion = p.Descripcion,
                        Precio = p.Precio,
                        EsAlmacenable = p.EsAlmacenable,
                        Cantidad = p.Cantidad,
                        RutaImagen = imagen?.Ruta,
                    };
                })
                .ToList(); // aca convertimos la lista de productos en una lista de DTOs

            // aca lo q vamos a hacer es devolver un JSON con la lista de productos y la paginación
            return Ok(
                new
                {
                    status = 200,
                    message = totalProductos > 0
                        ? "Lista obtenida correctamente."
                        : "No hay productos.",
                    pagination = new // creamos un objeto con la paginación que contiene el total de páginas, la página actual, el tamaño de la página y el total de productos
                    {
                        totalPages = (int)Math.Ceiling((double)totalProductos / pageSize), // aca obtenemos el total de páginas
                        currentPage = page, // aca obtenemos la página actual
                        pageSize, // aca obtenemos el tamaño de la página
                        totalProductos, // aca obtenemos el total de productos
                    },
                    productos = productosDto, // aca obtenemos la lista de productos con la paginación y la imagen de cada producto
                }
            );
        }

        // GET: api/productosservicios/noAlmacenables (Obtener todos los servicios)
        [HttpGet("noAlmacenables")]
        [Authorize(Roles = "Administrador,Barbero")]
        public IActionResult GetProductosNoAlmacenables(
            int page = 1,
            int pageSize = 10,
            string? sort = null,
            string? order = null
        )
        {
            var query = _context.ProductosServicios.Where(p =>
                p.Activo && !p.EsAlmacenable
            );

            // Aplicar ordenamiento
            switch (sort?.ToLower())
            {
                case "nombre":
                    query =
                        order == "desc"
                            ? query.OrderByDescending(p => p.Nombre)
                            : query.OrderBy(p => p.Nombre);
                    break;

                case "precio":
                    query =
                        order == "desc"
                            ? query.OrderByDescending(p => p.Precio)
                            : query.OrderBy(p => p.Precio);
                    break;

                case "descripcion":
                    query =
                        order == "desc"
                            ? query.OrderByDescending(p => p.Descripcion)
                            : query.OrderBy(p => p.Descripcion);
                    break;

                default:
                    // Orden por defecto
                    query = query.OrderBy(p => p.Nombre);
                    break;
            }

            var totalProductos = query.Count();
            var productosDb = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var productosDto = productosDb
                .Select(p =>
                {
                    var imagen = _context.Imagen.FirstOrDefault(i =>
                        i.TipoImagen == "ProductoServicio"
                        && i.IdRelacionado == p.Id
                        && i.Activo == true
                    );

                    return new ProductoServicioConImagenDto
                    {
                        Id = p.Id,
                        Nombre = p.Nombre,
                        Descripcion = p.Descripcion,
                        Precio = p.Precio,
                        EsAlmacenable = p.EsAlmacenable,
                        Cantidad = p.Cantidad,
                        RutaImagen = imagen?.Ruta,
                    };
                })
                .ToList();

            return Ok(
                new
                {
                    status = 200,
                    message = totalProductos > 0
                        ? "Lista de servicios obtenida correctamente."
                        : "No hay servicios disponibles.",
                    pagination = new
                    {
                        totalPages = (int)Math.Ceiling((double)totalProductos / pageSize),
                        currentPage = page,
                        pageSize,
                        totalProductos,
                    },
                    productos = productosDto,
                }
            );
        }

        // GET: api/productosservicios/venta?nombre=a
        [HttpGet("venta")]
        [Authorize(Roles = "Administrador,Barbero")]
        public IActionResult GetParaVenta(
            int page = 1,
            int pageSize = 10,
            [FromQuery] string? nombre = null
        )
        {
            // Verificar si _context o _context.ProductosServicios son null
            if (_context == null || _context.ProductosServicios == null)
            {
                return NotFound("No se encontraron productos o servicios.");
            }

            var query = _context.ProductosServicios.Where(p => p.Activo);

            if (!string.IsNullOrEmpty(nombre))
            {
                query = query.Where(p =>
                    EF.Functions.Like((p.Nombre ?? "").ToLower(), $"%{nombre.ToLower()}%")
                );
            }

            var total = query.Count();

            var items = query
                .OrderBy(p => p.Nombre)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ProductoServicioVentaDto
                {
                    Id = p.Id,
                    Nombre = p.Nombre ?? "Desconocido", // Valor por defecto
                    Precio = p.Precio ?? 0m, // Valor por defecto
                    EsAlmacenable = p.EsAlmacenable, // Valor por defecto
                })
                .ToList();

            return Ok(
                new
                {
                    status = 200,
                    message = total > 0
                        ? "Lista obtenida correctamente."
                        : "No hay productos ni servicios.",
                    pagination = new
                    {
                        totalPages = (int)Math.Ceiling((double)total / pageSize),
                        currentPage = page,
                        pageSize,
                        total,
                    },
                    productos = items,
                }
            );
        }

        // GET: api/productosservicios/{id}
        [HttpGet("{id}")]
        [Authorize(Roles = "Administrador,Barbero")]
        public async Task<IActionResult> GetProductoServicio(int id)
        {
            var productoServicio = await _context
                .ProductosServicios.Where(p => p.Activo && p.Id == id)
                .FirstOrDefaultAsync();
            if (productoServicio == null)
            {
                return NotFound(
                    new { status = 404, message = "El producto o servicio no existe." }
                );
            }

            // Buscamos imagen relacionada
            var imagen = _context.Imagen.FirstOrDefault(i =>
                i.TipoImagen == "ProductoServicio" && i.IdRelacionado == id && i.Activo == true
            );

            return Ok(
                new
                {
                    status = 200,
                    message = "Producto o servicio encontrado.",
                    productoServicio,
                    imagen,
                }
            );
        }

        // POST: api/productosservicios
        [HttpPost]
        [Authorize(Roles = "Administrador,Barbero")]
        public async Task<IActionResult> PostProductoServicio(
            [FromForm] ProductoServicioCrearDto dto
        )
        {
            if (string.IsNullOrEmpty(dto.Nombre))
                return BadRequest(new { status = 400, message = "El nombre es obligatorio." });

            if (!dto.EsAlmacenable && dto.Cantidad > 0)
                return BadRequest(
                    new { status = 400, message = "Un servicio no puede tener cantidad > 0." }
                );

            var producto = new ProductoServicio
            {
                Nombre = dto.Nombre,
                Descripcion = dto.Descripcion,
                Precio = dto.Precio,
                EsAlmacenable = dto.EsAlmacenable,
                Cantidad = dto.Cantidad,
            };

            _context.ProductosServicios.Add(producto);
            await _context.SaveChangesAsync();

            if (dto.Imagen != null && dto.Imagen.Length > 0)
            {
                // Carpeta base según tipo (producto o servicio)
                string baseFolder = producto.EsAlmacenable ? "productos" : "servicios";

                // Ruta completa: wwwroot/images/productos/{id} o wwwroot/images/servicios/{id}
                var uploadsPath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot",
                    "images",
                    baseFolder,
                    producto.Id.ToString()
                );

                Directory.CreateDirectory(uploadsPath); // crea carpeta si no existe

                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(dto.Imagen.FileName)}";
                var fullPath = Path.Combine(uploadsPath, fileName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await dto.Imagen.CopyToAsync(stream);
                }

                var nuevaImagen = new Imagen
                {
                    Ruta = $"/images/{baseFolder}/{producto.Id}/{fileName}",
                    TipoImagen = "ProductoServicio",
                    IdRelacionado = producto.Id,
                    Activo = true,
                    FechaCreacion = DateTime.UtcNow,
                };

                _context.Imagen.Add(nuevaImagen);
                await _context.SaveChangesAsync();
            }

            return CreatedAtAction(nameof(GetProductoServicio), new { id = producto.Id }, producto);
        }

        // PUT: api/productosservicios/{id}
        [HttpPut("{id}")]
        [Authorize(Roles = "Administrador,Barbero")]
        public async Task<IActionResult> PutProductoServicio(
            int id,
            [FromForm] ProductoServicioConImagenDto dto
        )
        {
            if (id != dto.Id)
            {
                return BadRequest(
                    new
                    {
                        status = 400,
                        message = "El ID en la URL no coincide con el ID del cuerpo de la solicitud.",
                    }
                );
            }

            var productoServicio = await _context.ProductosServicios.FindAsync(id);
            if (productoServicio == null)
            {
                return NotFound(
                    new { status = 404, message = "El producto o servicio no existe." }
                );
            }

            if (!dto.EsAlmacenable && dto.Cantidad > 0)
            {
                return BadRequest(
                    new
                    {
                        status = 400,
                        message = "Un producto no almacenable no puede tener cantidad mayor a 0.",
                    }
                );
            }

            // Actualizar campos del producto o servicio
            productoServicio.Nombre = dto.Nombre;
            productoServicio.Descripcion = dto.Descripcion;
            productoServicio.Precio = dto.Precio;
            productoServicio.EsAlmacenable = dto.EsAlmacenable;
            productoServicio.Cantidad = dto.Cantidad;

            // Buscar imagen activa actual (si existe)
            var imagenExistente = _context.Imagen.FirstOrDefault(i =>
                i.TipoImagen == "ProductoServicio" && i.IdRelacionado == id && i.Activo == true
            );

            // SUBIÓ UNA NUEVA IMAGEN
            if (dto.Imagen != null && dto.Imagen.Length > 0)
            {
                string baseFolder =
                    productoServicio.EsAlmacenable ? "productos" : "servicios";
                var carpetaPath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot",
                    "images",
                    baseFolder,
                    id.ToString()
                );
                Directory.CreateDirectory(carpetaPath);

                // Generar nuevo nombre de archivo
                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(dto.Imagen.FileName)}";
                var fullPath = Path.Combine(carpetaPath, fileName);

                // Guardar imagen nueva en disco
                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await dto.Imagen.CopyToAsync(stream);
                }

                if (imagenExistente != null)
                {
                    // Eliminar imagen anterior físicamente si existe
                    var rutaFisicaAnterior = Path.Combine(
                        Directory.GetCurrentDirectory(),
                        "wwwroot",
                        imagenExistente
                            .Ruta.TrimStart('/')
                            .Replace('/', Path.DirectorySeparatorChar)
                    );
                    if (System.IO.File.Exists(rutaFisicaAnterior))
                    {
                        System.IO.File.Delete(rutaFisicaAnterior);
                    }

                    // Actualizar imagen en DB
                    imagenExistente.Ruta = $"/images/{baseFolder}/{id}/{fileName}";
                    imagenExistente.FechaCreacion = DateTime.UtcNow;
                    _context.Imagen.Update(imagenExistente);
                }
                else
                {
                    // Crear nueva imagen en DB
                    var nuevaImagen = new Imagen
                    {
                        Ruta = $"/images/{baseFolder}/{id}/{fileName}",
                        TipoImagen = "ProductoServicio",
                        IdRelacionado = id,
                        Activo = true,
                        FechaCreacion = DateTime.UtcNow,
                    };
                    _context.Imagen.Add(nuevaImagen);
                }
            }

            // ELIMINÓ LA IMAGEN SIN SUBIR UNA NUEVA
            if (dto.EliminarImagen == true && imagenExistente != null)
            {
                var rutaFisicaOriginal = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot",
                    imagenExistente.Ruta.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)
                );

                if (System.IO.File.Exists(rutaFisicaOriginal))
                {
                    var rutaRelativaTrash = imagenExistente.Ruta.Replace("/images/", "/trash/");
                    var rutaFisicaTrash = Path.Combine(
                        Directory.GetCurrentDirectory(),
                        "wwwroot",
                        rutaRelativaTrash.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)
                    );

                    var carpetaTrash = Path.GetDirectoryName(rutaFisicaTrash);
                    if (!string.IsNullOrEmpty(carpetaTrash) && !Directory.Exists(carpetaTrash))
                    {
                        Directory.CreateDirectory(carpetaTrash);
                    }

                    System.IO.File.Move(rutaFisicaOriginal, rutaFisicaTrash);

                    imagenExistente.Ruta = rutaRelativaTrash;
                    imagenExistente.Activo = false;

                    _context.Imagen.Update(imagenExistente);
                }
            }

            _context.ProductosServicios.Update(productoServicio);
            await _context.SaveChangesAsync();

            return Ok(
                new
                {
                    status = 200,
                    message = "Producto o servicio actualizado correctamente.",
                    productoServicio,
                }
            );
        }

        // DELETE: api/productosservicios/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProductoServicio(int id)
        {
            var productoServicio = await _context.ProductosServicios.FindAsync(id);
            if (productoServicio == null)
            {
                return NotFound(
                    new { status = 404, message = "El producto o servicio no existe." }
                );
            }

            var tieneDependencias = _context.DetalleAtencion.Any(d => d.ProductoServicioId == id);
            if (tieneDependencias)
            {
                return BadRequest(
                    new
                    {
                        status = 400,
                        message = "No se puede eliminar el producto porque está vinculado a una atención.",
                    }
                );
            }

            // Cambio lógico en BD
            productoServicio.Activo = false;
            _context.ProductosServicios.Update(productoServicio);

            // Mover imágenes a trash y actualizar activo a false
            var imagenes = _context
                .Imagen.Where(i => i.TipoImagen == "ProductoServicio" && i.IdRelacionado == id)
                .ToList();

            foreach (var imagen in imagenes)
            {
                var rutaFisicaOriginal = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot",
                    imagen.Ruta.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)
                );
                if (System.IO.File.Exists(rutaFisicaOriginal))
                {
                    // Crear ruta para trash
                    var rutaRelativaTrash = imagen.Ruta.Replace("/images/", "/trash/");
                    var rutaFisicaTrash = Path.Combine(
                        Directory.GetCurrentDirectory(),
                        "wwwroot",
                        rutaRelativaTrash.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)
                    );

                    // Crear carpeta trash si no existe
                    var carpetaTrash = Path.GetDirectoryName(rutaFisicaTrash);
                    if (!string.IsNullOrEmpty(carpetaTrash) && !Directory.Exists(carpetaTrash))
                    {
                        Directory.CreateDirectory(carpetaTrash);
                    }

                    // Mover archivo
                    System.IO.File.Move(rutaFisicaOriginal, rutaFisicaTrash);

                    // Actualizar ruta en BD
                    imagen.Ruta = rutaRelativaTrash;

                    // Marcar imagen como inactiva (opcional)
                    imagen.Activo = false;

                    _context.Imagen.Update(imagen);
                }
            }

            await _context.SaveChangesAsync();

            return Ok(
                new
                {
                    status = 200,
                    message = "Producto o servicio eliminado lógicamente y sus imágenes movidas a trash.",
                    productoEliminado = productoServicio.Nombre,
                }
            );
        }

        // DELETE: api/productosservicios/imagen/{idImagen}
        [HttpDelete("imagen/{idImagen}")]
        public async Task<IActionResult> DeleteImagen(int idImagen)
        {
            var imagen = await _context.Imagen.FindAsync(idImagen);
            if (imagen == null)
            {
                return NotFound(new { status = 404, message = "La imagen no existe." });
            }

            var producto = await _context.ProductosServicios.FindAsync(imagen.IdRelacionado);
            if (producto == null)
            {
                return BadRequest(
                    new { status = 400, message = "El producto o servicio relacionado no existe." }
                );
            }

            // Usa la misma lógica que el método que sí funciona
            var rutaFisicaOriginal = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                imagen.Ruta.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)
            );

            if (!System.IO.File.Exists(rutaFisicaOriginal))
            {
                return NotFound(
                    new { status = 404, message = "El archivo físico de la imagen no existe." }
                );
            }

            // Nueva ruta relativa para trash
            var rutaRelativaTrash = imagen.Ruta.Replace("/images/", "/trash/");
            var rutaFisicaTrash = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                rutaRelativaTrash.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)
            );

            // Crear carpeta trash si no existe
            var carpetaTrash = Path.GetDirectoryName(rutaFisicaTrash);
            if (!string.IsNullOrEmpty(carpetaTrash) && !Directory.Exists(carpetaTrash))
            {
                Directory.CreateDirectory(carpetaTrash);
            }

            // Mover archivo
            System.IO.File.Move(rutaFisicaOriginal, rutaFisicaTrash);

            // Actualizar datos en la base
            imagen.Ruta = rutaRelativaTrash;
            imagen.Activo = false;

            _context.Imagen.Update(imagen);
            await _context.SaveChangesAsync();

            return Ok(
                new { status = 200, message = "Imagen movida a trash y desactivada correctamente." }
            );
        }

        // GET: api/productosservicios/{id}/imagen
        [HttpGet("{id}/imagen")]
        public IActionResult GetImagen(int id)
        {
            var imagen = _context.Imagen.FirstOrDefault(i =>
                i.TipoImagen == "ProductoServicio" && i.IdRelacionado == id && i.Activo == true
            );

            if (imagen == null || string.IsNullOrEmpty(imagen.Ruta))
            {
                return NotFound(
                    new
                    {
                        status = 404,
                        message = "No hay imagen disponible para este producto o servicio.",
                    }
                );
            }

            return PhysicalFile(Path.Combine("wwwroot", imagen.Ruta.TrimStart('/')), "image/jpeg");
        }
    }
}
