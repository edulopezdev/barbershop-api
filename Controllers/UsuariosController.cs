using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using backend.Data;
using backend.Dtos;
using backend.Extensions;
using backend.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // protección general de todos los endpoints
    // este es el controlador de usuarios q hereda de ControllerBase
    public class UsuariosController : ControllerBase
    {
        private readonly ApplicationDbContext _context; // aca se define el contexto para interactuar con la BD

        public UsuariosController(ApplicationDbContext context)
        {
            _context = context; // se inyecta el contexto para interactuar con la BD
        }

        // GET: api/usuarios (lista de usuarios con roles distintos a 3)
        [HttpGet]
        [Authorize(Roles = "Administrador")] // protección de endpoint, solo los admin tienen acceso
        public IActionResult GetUsuarios(
            int page = 1, // este es elvalor por defecto para la paginación, es decir que se inicia en la primera página
            int pageSize = 10, // vamos a mostrar 10 usuarios por página
            string? nombre = null, // el nombre puede q llegue por query string desde el frontend
            string? email = null, // el email puede q llegue por query string desde el frontend
            string? telefono = null, // el telefono puede q llegue por query string desde el frontend
            string? rolNombre = null, // el rol puede q llegue por query string desde el frontend
            bool? activo = null // el activo puede q llegue por query string desde el frontend
        ) // se definen los parámetros de paginación y filtrado q se reciben por query string desde el frontend
        {
            //aca lo q hago es traer todos los usuarios q no sean clientes, es decir con RolId distinto de 3
            var query = _context.Usuario.Include(u => u.Rol).Where(u => u.RolId != 3).AsQueryable();

            // esto es para filtrar por nombre, email, telefono, rol y activo
            string? ordenarPor = HttpContext.Request.Query["ordenarPor"]; // se obtiene el parámetro de ordenamiento desde el query string
            string? ordenarDesc = HttpContext.Request.Query["ordenDescendente"]; // se obtiene el parámetro de ordenamiento descendente desde el query string
            bool ordenarDescendente = ordenarDesc == "true";

            if (!string.IsNullOrEmpty(ordenarPor)) // si el parámetro de ordenamiento no es nulo
            {
                ordenarPor = ordenarPor.ToLower(); // convertimos a minuscula el parámetro de ordenamiento
                query = ordenarPor switch
                {
                    "nombre" => ordenarDescendente
                        ? query.OrderByDescending(u => u.Nombre)
                        : query.OrderBy(u => u.Nombre),
                    "email" => ordenarDescendente
                        ? query.OrderByDescending(u => u.Email)
                        : query.OrderBy(u => u.Email),
                    "telefono" => ordenarDescendente
                        ? query.OrderByDescending(u => u.Telefono)
                        : query.OrderBy(u => u.Telefono),
                    "activo" => ordenarDescendente
                        ? query.OrderByDescending(u => u.Activo)
                        : query.OrderBy(u => u.Activo),
                    "rolnombre" => ordenarDescendente
                        ? query.OrderByDescending(u => u.Rol!.NombreRol)
                        : query.OrderBy(u => u.Rol!.NombreRol),
                    _ => query,
                };
            }
            else
            {
                query = query.OrderBy(u => u.Nombre); // si no llego nada, por default se ordena por nombre
            }

            // Filtros por nombre, email, telefono, rol y activo
            if (!string.IsNullOrEmpty(nombre))
                query = query.Where(u => u.Nombre!.Contains(nombre));
            if (!string.IsNullOrEmpty(email))
                query = query.Where(u => u.Email!.Contains(email));
            if (!string.IsNullOrEmpty(telefono))
                query = query.Where(u => u.Telefono!.Contains(telefono));
            if (!string.IsNullOrEmpty(rolNombre))
                query = query.Where(u => u.Rol!.NombreRol.Contains(rolNombre));
            if (activo.HasValue)
                query = query.Where(u => u.Activo == activo.Value);

            var total = query.Count(); // aca se obtiene el total de usuarios para la paginación

            var data = query // aca se hace la paginación
                .Skip((page - 1) * pageSize) // .skip lo q hace es saltarse los primero 10 si estamos en la segunda pagina
                .Take(pageSize) //.take lo q hace es tomar los siguientes 10
                .Select(u => new UsuarioDto //aca vamos a crear una lista de objetos UsuarioDto con los datos q necesitamos
                {
                    Id = u.Id,
                    Nombre = u.Nombre!,
                    Email = u.Email == null ? null : u.Email,
                    Telefono = u.Telefono!,
                    Activo = u.Activo,
                    RolId = u.RolId,
                    RolNombre = u.Rol!.NombreRol,
                })
                .ToList(); // finalmente se convierte en una lista

            string mensaje =
                total > 0
                    ? "Usuarios obtenidos correctamente."
                    : "No se encontraron usuarios con esos filtros.";

            return Ok(
                new
                {
                    status = 200,
                    message = mensaje,
                    pagination = new
                    {
                        totalPages = (int)Math.Ceiling((double)total / pageSize),
                        currentPage = page,
                        pageSize,
                        total,
                    },
                    usuarios = data,
                }
            );
        }

        // GET: api/usuarios/usuarios-sistema
        [HttpGet("usuarios-sistema")]
        [Authorize(Roles = "Administrador")]
        public IActionResult GetUsuariosSistema(int page = 1, int pageSize = 10)
        {
            var query = _context.Usuario.Where(u => u.Activo && (u.RolId == 1 || u.RolId == 2));

            var total = query.Count();
            var data = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return Ok(
                new
                {
                    status = 200,
                    message = "Usuarios obtenidos correctamente.",
                    pagination = new
                    {
                        totalPages = (int)Math.Ceiling((double)total / pageSize),
                        currentPage = page,
                        pageSize,
                        total,
                    },
                    usuarios = data,
                }
            );
        }

        // GET: api/usuarios/clientes (lista de clientes)
        [HttpGet("clientes")]
        [Authorize(Roles = "Administrador,Barbero")]
        public IActionResult GetClientes(
            int page = 1,
            int pageSize = 10,
            string? nombre = null,
            string? email = null,
            string? telefono = null,
            bool? activo = null
        )
        {
            // Base query: solo usuarios con RolId = 3 (clientes)
            var query = _context.Usuario.AsQueryable();

            query = query.Where(u => u.RolId == 3);

            // Obtener ordenamiento desde query string manualmente
            string? ordenarPor = HttpContext.Request.Query["ordenarPor"];
            string? ordenarDesc = HttpContext.Request.Query["ordenDescendente"];
            bool ordenarDescendente = ordenarDesc == "true";

            if (!string.IsNullOrEmpty(ordenarPor))
            {
                ordenarPor = ordenarPor.ToLower(); // normalizar

                query = ordenarPor switch
                {
                    "nombre" => ordenarDescendente
                        ? query.OrderByDescending(u => u.Nombre)
                        : query.OrderBy(u => u.Nombre),
                    "email" => ordenarDescendente
                        ? query.OrderByDescending(u => u.Email)
                        : query.OrderBy(u => u.Email),
                    "telefono" => ordenarDescendente
                        ? query.OrderByDescending(u => u.Telefono)
                        : query.OrderBy(u => u.Telefono),
                    "activo" => ordenarDescendente
                        ? query.OrderByDescending(u => u.Activo)
                        : query.OrderBy(u => u.Activo),
                    _ => query, // si el campo no es válido, no se ordena
                };
            }

            // Filtros dinámicos
            if (!string.IsNullOrEmpty(nombre))
            {
                query = query.Where(u => u.Nombre!.Contains(nombre));
            }
            if (!string.IsNullOrEmpty(email))
            {
                query = query.Where(u => u.Email!.Contains(email));
            }
            if (!string.IsNullOrEmpty(telefono))
            {
                query = query.Where(u => u.Telefono!.Contains(telefono));
            }
            if (activo.HasValue)
            {
                query = query.Where(u => u.Activo == activo.Value);
            }

            var total = query.Count();

            var data = query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new ClienteDto
                {
                    Id = u.Id,
                    Nombre = u.Nombre!,
                    Email = u.Email == null ? null : u.Email,
                    Telefono = u.Telefono!,
                    Activo = u.Activo,
                })
                .ToList();

            string mensaje =
                total > 0
                    ? "Clientes obtenidos correctamente."
                    : "No se encontraron clientes con esos filtros.";

            return Ok(
                new
                {
                    status = 200,
                    message = mensaje,
                    pagination = new
                    {
                        totalPages = (int)Math.Ceiling((double)total / pageSize),
                        currentPage = page,
                        pageSize,
                        total,
                    },
                    clientes = data,
                }
            );
        }

        // GET: api/usuarios/{id} (Un usuario específico)
        [HttpGet("{id}")]
        [Authorize(Roles = "Administrador,Barbero")]
        public async Task<IActionResult> GetUsuario(int id)
        {
            var usuario = await _context
                .Usuario.Include(u => u.Rol)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (usuario == null)
            {
                return NotFound(
                    new
                    {
                        status = 404,
                        error = "Usuario no encontrado",
                        message = "No se encontró ningún usuario con el ID proporcionado.",
                    }
                );
            }

            var rolActual = User.GetUserRole();

            // Barbero solo puede ver clientes
            if (rolActual == "Barbero" && usuario.RolId != 3)
            {
                return Forbid("No tienes permiso para ver este usuario.");
            }

            var usuarioDto = new UsuarioDto
            {
                Id = usuario.Id,
                Nombre = usuario.Nombre,
                Email = usuario.Email,
                Telefono = usuario.Telefono,
                Avatar = !string.IsNullOrEmpty(usuario.Avatar)
                    ? usuario.Avatar
                    : "/avatars/no_avatar.jpg",
                RolId = usuario.RolId,
                AccedeAlSistema = usuario.AccedeAlSistema,
                Activo = usuario.Activo,
                FechaRegistro = usuario.FechaRegistro,
                FechaModificacion = usuario.FechaModificacion,
                RolNombre = usuario.Rol?.NombreRol ?? string.Empty,
            };

            return Ok(
                new
                {
                    status = 200,
                    message = "Usuario encontrado.",
                    usuario = usuarioDto,
                }
            );
        }

        // POST: api/usuarios (Crear un nuevo usuario)
        [HttpPost]
        [Authorize(Roles = "Administrador,Barbero")]
        public IActionResult PostUsuario([FromBody] CrearUsuarioDto dto)
        {
            try
            {
                var usuarioIdLogueado = User.GetUserId() ?? 0;
                var rolActual = User.FindFirst(ClaimTypes.Role)?.Value;

                if (!ModelState.IsValid)
                {
                    return BadRequest(
                        new
                        {
                            status = 400,
                            error = "Datos inválidos",
                            message = "Uno o más campos son incorrectos.",
                            errors = ModelState,
                        }
                    );
                }

                // Validar que un Barbero solo cree clientes
                if (rolActual == "Barbero" && dto.RolId != 3)
                {
                    return Forbid("No tienes permiso para crear usuarios con este rol.");
                }

                // Validar email duplicado
                var emailNormalizado = dto.Email?.Trim().ToLower();
                bool emailExistente = _context.Usuario.Any(u =>
                    u.Email != null && u.Email.ToLower() == emailNormalizado
                );

                if (emailExistente)
                {
                    return BadRequest(
                        new { status = 400, message = "El email ya está en uso por otro usuario." }
                    );
                }

                var usuario = new Usuario
                {
                    Nombre = dto.Nombre,
                    Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim(),
                    Telefono = dto.Telefono,
                    Avatar = dto.Avatar,
                    RolId = dto.RolId,
                    AccedeAlSistema = dto.AccedeAlSistema,
                    Activo = true,
                    FechaRegistro = DateTime.UtcNow,
                    IdUsuarioCrea = usuarioIdLogueado,
                };

                if (usuario.RolId == 3)
                {
                    usuario.AccedeAlSistema = false; // clientes no acceden al sistema
                }

                if (usuario.AccedeAlSistema)
                {
                    if (string.IsNullOrEmpty(dto.Password))
                    {
                        return BadRequest(
                            new
                            {
                                status = 400,
                                message = "La contraseña es obligatoria si el usuario accede al sistema.",
                            }
                        );
                    }
                    usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
                }
                else
                {
                    usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword("123"); // password por defecto para clientes
                }

                _context.Usuario.Add(usuario);
                _context.SaveChanges();

                return CreatedAtAction(
                    nameof(GetUsuario),
                    new { id = usuario.Id },
                    new
                    {
                        status = 201,
                        message = "Usuario creado exitosamente.",
                        usuario = new
                        {
                            usuario.Id,
                            usuario.Nombre,
                            usuario.Email,
                            usuario.Telefono,
                            usuario.Avatar,
                            usuario.RolId,
                            usuario.AccedeAlSistema,
                            usuario.Activo,
                            usuario.FechaRegistro,
                            usuario.IdUsuarioCrea,
                        },
                    }
                );
            }
            catch (Exception ex)
            {
                return StatusCode(
                    500,
                    new { status = 500, message = "Error interno: " + ex.Message }
                );
            }
        }

        // PUT: api/usuarios/{id}
        [HttpPut("{id}")]
        [Authorize(Roles = "Administrador,Barbero")]
        public IActionResult PutUsuario(int id, [FromBody] EditarUsuarioDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(
                    new
                    {
                        status = 400,
                        code = "DATOS_INVALIDOS",
                        message = "Datos inválidos",
                        errors = ModelState,
                    }
                );
            }

            var usuarioIdLogueadoString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int usuarioIdLogueado = 0;
            if (
                !string.IsNullOrEmpty(usuarioIdLogueadoString)
                && !int.TryParse(usuarioIdLogueadoString, out usuarioIdLogueado)
            )
            {
                usuarioIdLogueado = 0;
            }

            var esAdministrador = User.IsInRole("Administrador");
            var esBarbero = User.IsInRole("Barbero");

            var usuarioExistente = _context.Usuario.Find(id);
            if (usuarioExistente == null)
            {
                return NotFound(
                    new
                    {
                        status = 404,
                        code = "USUARIO_NO_ENCONTRADO",
                        message = "El usuario no existe.",
                    }
                );
            }

            // Validaciones de permisos
            if (esBarbero)
            {
                // Barbero solo puede editar clientes
                if (usuarioExistente.RolId != 3)
                {
                    return Forbid("No tienes permiso para modificar este usuario.");
                }

                // No puede cambiar el rol del cliente (debe seguir siendo cliente)
                if (dto.RolId != 3)
                {
                    return Forbid("No puedes cambiar el rol de un cliente.");
                }

                // No puede cambiar su propio rol (aunque en lógica solo puede editar clientes, por seguridad)
                if (usuarioExistente.Id == usuarioIdLogueado && dto.RolId != usuarioExistente.RolId)
                {
                    return Forbid("No puedes cambiar tu propio rol.");
                }
            }
            else if (!esAdministrador)
            {
                // Si no es admin ni barbero, no tiene permiso
                return Unauthorized(
                    new
                    {
                        status = 401,
                        code = "SIN_AUTORIZACION",
                        message = "No tienes permisos para modificar este usuario.",
                    }
                );
            }

            // Validar email duplicado
            var nuevoEmail = dto.Email?.Trim().ToLower();
            var emailActual = usuarioExistente.Email?.Trim().ToLower();

            if (nuevoEmail != emailActual)
            {
                bool emailDuplicado = _context.Usuario.Any(u =>
                    u.Email != null && u.Email.ToLower() == nuevoEmail && u.Id != id
                );

                if (emailDuplicado)
                {
                    return BadRequest(
                        new
                        {
                            status = 400,
                            code = "EMAIL_DUPLICADO",
                            message = "El email ya está en uso por otro usuario.",
                        }
                    );
                }
            }

            // Actualizar campos
            usuarioExistente.Nombre = dto.Nombre;
            usuarioExistente.Email = dto.Email?.Trim() ?? usuarioExistente.Email;
            usuarioExistente.Telefono = dto.Telefono;
            usuarioExistente.Avatar = dto.Avatar;

            // Evitar que un administrador se cambie su propio rol
            if (
                esAdministrador
                && usuarioExistente.Id == usuarioIdLogueado
                && dto.RolId != usuarioExistente.RolId
            )
            {
                return BadRequest(
                    new { status = 400, message = "No puedes cambiar tu propio rol." }
                );
            }

            // Solo admin puede cambiar rol
            if (esAdministrador)
            {
                usuarioExistente.RolId = dto.RolId;
            }

            usuarioExistente.AccedeAlSistema = dto.AccedeAlSistema;
            usuarioExistente.Activo = dto.Activo ?? usuarioExistente.Activo;
            usuarioExistente.IdUsuarioModifica = usuarioIdLogueado;
            usuarioExistente.FechaModificacion = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(dto.Password))
            {
                usuarioExistente.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
            }

            _context.SaveChanges();

            return Ok(
                new
                {
                    status = 200,
                    message = "Usuario actualizado correctamente.",
                    usuario = new
                    {
                        usuarioExistente.Id,
                        usuarioExistente.Nombre,
                        usuarioExistente.Email,
                        usuarioExistente.Telefono,
                        usuarioExistente.Avatar,
                        usuarioExistente.RolId,
                        usuarioExistente.AccedeAlSistema,
                        usuarioExistente.Activo,
                        usuarioExistente.FechaRegistro,
                        usuarioExistente.FechaModificacion,
                    },
                }
            );
        }

        // DELETE: api/usuarios/{id} (Eliminación lógica)
        [HttpDelete("{id}")]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> DeleteUsuario(int id)
        {
            var idUsuarioActual = User.GetUserId();

            if (idUsuarioActual == null)
            {
                return Unauthorized(
                    new
                    {
                        status = 401,
                        error = "No autorizado",
                        message = "No se pudo determinar la identidad del usuario actual.",
                    }
                );
            }

            if (id == idUsuarioActual)
            {
                return BadRequest(
                    new
                    {
                        status = 400,
                        error = "Operación no permitida",
                        message = "No puedes eliminar tu propio usuario mientras estás logueado.",
                    }
                );
            }

            var usuario = await _context.Usuario.FindAsync(id);
            if (usuario == null)
            {
                return NotFound(
                    new
                    {
                        status = 404,
                        error = "No encontrado",
                        message = "El usuario que intentas eliminar no existe.",
                    }
                );
            }

            if (!usuario.Activo)
            {
                return BadRequest(
                    new
                    {
                        status = 400,
                        error = "Operación no permitida",
                        message = "El usuario ya está inactivo.",
                    }
                );
            }

            // Validar que si es admin, no sea el último activo
            if (usuario.RolId == 1) // Asumiendo 1 = Administrador
            {
                var adminsActivos = _context.Usuario.Count(u =>
                    u.RolId == 1 && u.Activo && u.Id != id
                );
                if (adminsActivos == 0)
                {
                    return BadRequest(
                        new
                        {
                            status = 400,
                            error = "Operación no permitida",
                            message = "No se puede eliminar al último administrador activo.",
                        }
                    );
                }
            }

            usuario.Activo = false;
            await _context.SaveChangesAsync();

            return Ok(
                new
                {
                    status = 200,
                    message = "El usuario ha sido marcado como inactivo correctamente.",
                    usuario = new
                    {
                        usuario.Id,
                        usuario.Nombre,
                        usuario.Email,
                        usuario.RolId,
                        usuario.Activo,
                    },
                }
            );
        }

        // PATCH: api/usuarios/{id}/estado
        [HttpPatch("{id}/estado")]
        [Authorize(Roles = "Administrador,Barbero")]
        public async Task<IActionResult> CambiarEstadoUsuario(
            int id,
            [FromBody] CambiarEstadoDto dto
        )
        {
            if (dto == null)
            {
                return BadRequest(new { status = 400, message = "Datos inválidos" });
            }

            var usuario = await _context.Usuario.FindAsync(id);
            if (usuario == null)
            {
                return BadRequest(
                    new
                    {
                        status = 400,
                        error = "Datos inválidos",
                        message = "Datos inválidos",
                    }
                );
            }

            var idUsuarioActual = User.GetUserId();
            if (idUsuarioActual == null)
            {
                return Unauthorized(
                    new
                    {
                        status = 401,
                        message = "No se pudo determinar la identidad del usuario actual.",
                    }
                );
            }

            if (id == idUsuarioActual && dto.Activo == false)
            {
                return BadRequest(
                    new { status = 400, message = "No puedes desactivar tu propio usuario." }
                );
            }

            if (usuario.Activo == dto.Activo)
            {
                return BadRequest(
                    new { status = 400, message = "El estado del usuario ya es el solicitado." }
                );
            }

            if (usuario.RolId == 1 && dto.Activo == false) // Asumiendo 1 = Administrador
            {
                var adminsActivos = _context.Usuario.Count(u =>
                    u.RolId == 1 && u.Activo && u.Id != id
                );
                if (adminsActivos == 0)
                {
                    return BadRequest(
                        new
                        {
                            status = 400,
                            message = "No se puede desactivar al último administrador activo.",
                        }
                    );
                }
            }

            usuario.Activo = dto.Activo;
            usuario.FechaModificacion = DateTime.UtcNow;
            usuario.IdUsuarioModifica = idUsuarioActual.Value;

            await _context.SaveChangesAsync();

            return Ok(
                new
                {
                    status = 200,
                    message = dto.Activo
                        ? "Usuario restaurado correctamente."
                        : "Usuario desactivado correctamente.",
                    usuario = new
                    {
                        usuario.Id,
                        usuario.Nombre,
                        usuario.Email,
                        usuario.Activo,
                    },
                }
            );
        }

        // Endpoint para obtener el perfil del usuario autenticado

        // GET: api/usuarios/perfil
        [HttpGet("perfil")]
        [Authorize(Roles = "Administrador,Barbero")]
        public IActionResult GetPerfil()
        {
            var userId = User.GetUserId();
            if (userId == null)
            {
                return Unauthorized(new { status = 401, message = "Usuario no autenticado." });
            }

            var usuario = _context
                .Usuario.Include(u => u.Rol)
                .FirstOrDefault(u => u.Id == userId.Value);

            if (usuario == null)
            {
                return NotFound(
                    new
                    {
                        status = 404,
                        error = "Usuario no encontrado",
                        message = "El usuario especificado no existe.",
                    }
                );
            }

            var perfilDto = new UsuarioPerfilDto
            {
                Id = usuario.Id,
                Nombre = usuario.Nombre!,
                Email = usuario.Email!,
                Telefono = usuario.Telefono,
                RolNombre = usuario.Rol!.NombreRol,
                AccedeAlSistema = usuario.AccedeAlSistema,
                Activo = usuario.Activo,
                FechaRegistro = usuario.FechaRegistro,
                Avatar = !string.IsNullOrEmpty(usuario.Avatar)
                    ? usuario.Avatar
                    : "/avatars/no_avatar.jpg",
            };

            return Ok(
                new
                {
                    status = 200,
                    message = "Perfil obtenido correctamente.",
                    usuario = perfilDto,
                }
            );
        }

        // PUT: api/usuarios/perfil
        [HttpPut("perfil")]
        [Authorize(Roles = "Administrador,Barbero")]
        public IActionResult UpdatePerfil()
        {
            try
            {
                var userId = User.GetUserId();
                if (userId == null)
                {
                    return Unauthorized(new { status = 401, message = "Usuario no autenticado." });
                }

                var form = Request.Form;

                var nombre = form["Nombre"].ToString();
                var email = form["Email"].ToString();
                var telefono = form["Telefono"].ToString();
                var password = form["Password"].ToString();
                var eliminarAvatar = form["EliminarAvatar"].ToString().ToLower() == "true";

                var usuario = _context
                    .Usuario.Include(u => u.Rol)
                    .FirstOrDefault(u => u.Id == userId.Value);

                if (usuario == null)
                {
                    return NotFound(new { status = 404, message = "Usuario no encontrado." });
                }

                // Validación de email duplicado
                var nuevoEmail = !string.IsNullOrEmpty(email) ? email.Trim().ToLower() : null;
                var emailActual = usuario.Email?.Trim().ToLower();

                if (nuevoEmail != emailActual)
                {
                    bool emailExistente = _context.Usuario.Any(u =>
                        u.Email != null && u.Email.ToLower() == nuevoEmail && u.Id != userId.Value
                    );

                    if (emailExistente)
                    {
                        return BadRequest(
                            new
                            {
                                status = 400,
                                message = "El email ya está en uso por otro usuario.",
                            }
                        );
                    }
                }

                // Actualizar datos del perfil
                usuario.Nombre = nombre;
                usuario.Email = email;
                usuario.Telefono = telefono;
                usuario.FechaModificacion = DateTime.UtcNow;
                usuario.IdUsuarioModifica = userId.Value;

                // Cambiar contraseña si se envía
                if (!string.IsNullOrWhiteSpace(password))
                {
                    usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
                }

                // Función local para eliminar avatar
                void EliminarArchivoAvatar(string avatarRuta)
                {
                    var oldFilePath = Path.Combine(
                        Directory.GetCurrentDirectory(),
                        "wwwroot",
                        avatarRuta.TrimStart('/')
                    );

                    if (System.IO.File.Exists(oldFilePath))
                    {
                        GC.Collect();
                        GC.WaitForPendingFinalizers();

                        var fileInfo = new FileInfo(oldFilePath);
                        fileInfo.Attributes = FileAttributes.Normal;
                        System.IO.File.Delete(oldFilePath);

                        var oldUserFolder = Path.GetDirectoryName(oldFilePath);
                        if (
                            Directory.Exists(oldUserFolder)
                            && !Directory.EnumerateFileSystemEntries(oldUserFolder).Any()
                        )
                        {
                            var dirInfo = new DirectoryInfo(oldUserFolder);
                            dirInfo.Attributes = FileAttributes.Normal;
                            Directory.Delete(oldUserFolder);
                        }
                    }
                }

                // Eliminar avatar si se solicita
                if (eliminarAvatar && !string.IsNullOrEmpty(usuario.Avatar))
                {
                    EliminarArchivoAvatar(usuario.Avatar);
                    usuario.Avatar = null;
                }

                // Subir nuevo avatar si hay archivo
                var avatarFile = Request.Form.Files.FirstOrDefault();

                if (avatarFile != null && avatarFile.Length > 0)
                {
                    // Si NO eliminamos el avatar y ya existe, eliminar el anterior para reemplazarlo
                    if (!eliminarAvatar && !string.IsNullOrEmpty(usuario.Avatar))
                    {
                        EliminarArchivoAvatar(usuario.Avatar);
                    }

                    var uploadsFolder = Path.Combine(
                        Directory.GetCurrentDirectory(),
                        "wwwroot",
                        "avatars"
                    );
                    var userFolderPath = Path.Combine(uploadsFolder, userId.Value.ToString());

                    if (!Directory.Exists(userFolderPath))
                    {
                        Directory.CreateDirectory(userFolderPath);
                    }

                    var fileName = $"avatar_{userId}_{Guid.NewGuid()}.jpg";
                    var filePath = Path.Combine(userFolderPath, fileName);

                    using (var image = Image.Load(avatarFile.OpenReadStream()))
                    {
                        var size = new Size(240, 240);
                        var resizeOptions = new ResizeOptions
                        {
                            Size = size,
                            Mode = ResizeMode.Crop, // Mantiene proporción y recorta centrado
                            Position = AnchorPositionMode.Center,
                        };

                        image.Mutate(x => x.Resize(resizeOptions));

                        var encoder = new JpegEncoder { Quality = 90 };
                        using (var outputStream = new FileStream(filePath, FileMode.Create))
                        {
                            image.Save(outputStream, encoder);
                        }
                    }

                    usuario.Avatar = $"/avatars/{userId}/{fileName}";
                }

                _context.SaveChanges();

                return Ok(
                    new
                    {
                        status = 200,
                        message = "Perfil actualizado correctamente.",
                        usuario = new UsuarioPerfilDto
                        {
                            Id = usuario.Id,
                            Nombre = usuario.Nombre!,
                            Email = usuario.Email!,
                            Telefono = usuario.Telefono,
                            RolNombre = usuario.Rol!.NombreRol,
                            AccedeAlSistema = usuario.AccedeAlSistema,
                            Activo = usuario.Activo,
                            FechaRegistro = usuario.FechaRegistro,
                            Avatar = !string.IsNullOrEmpty(usuario.Avatar)
                                ? usuario.Avatar
                                : "/avatars/no_avatar.jpg",
                        },
                    }
                );
            }
            catch (Exception ex)
            {
                return StatusCode(
                    500,
                    new { status = 500, message = "Error interno: " + ex.Message }
                );
            }
        }
    }
}
