using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using backend.Data;
using backend.Dtos;
using backend.Models;
using backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace backend.Controllers.Public
{
    [ApiController]
    [Route("api/public/[controller]")]
    public class AutenticacionController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailSender _emailSender;
        private readonly IVerificacionService _verificacionService;
        private readonly IConfiguration _config;
        private readonly ILogger<AutenticacionController> _logger;

        public AutenticacionController(
            ApplicationDbContext context,
            IEmailSender emailSender,
            IVerificacionService verificacionService,
            IConfiguration config,
            ILogger<AutenticacionController> logger
        )
        {
            _context = context;
            _emailSender = emailSender;
            _verificacionService = verificacionService;
            _config = config;
            _logger = logger;
        }

        // ========================================
        // Endpoints de Registro
        // ========================================

        /// <summary>
        /// Registra un nuevo usuario y envía código de verificación por email.
        /// POST /api/public/Autenticacion/Registro
        /// </summary>
        [HttpPost("Registro")]
        [AllowAnonymous]
        public async Task<IActionResult> PostRegistro([FromBody] PublicRegistroDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Validar email duplicado
            if (
                !string.IsNullOrEmpty(dto.Email)
                && await _context.Usuario.AnyAsync(u => u.Email == dto.Email)
            )
            {
                return BadRequest(new { success = false, error = "El email ya está registrado." });
            }

            var usuario = new Usuario
            {
                Nombre = dto.Nombre,
                Email = dto.Email,
                Telefono = dto.Telefono,
                RolId = 3,
                AccedeAlSistema = true,
                Activo = false,
                FechaRegistro = DateTime.UtcNow,
                IdUsuarioCrea = 0,
            };

            // ✅ Usar BCrypt de manera consistente con AuthController
            usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

            _context.Usuario.Add(usuario);
            await _context.SaveChangesAsync();

            // Generar código de 4 dígitos
            int codeInt = RandomNumberGenerator.GetInt32(0, 10_000);
            string code = codeInt.ToString("D4");

            byte[] codeHash;
            using (var sha = SHA256.Create())
                codeHash = sha.ComputeHash(Encoding.UTF8.GetBytes(code));

            var verificacion = new VerificacionEmail
            {
                UsuarioId = usuario.Id,
                CodeHash = codeHash,
                Expiracion = DateTime.UtcNow.AddHours(24),
                CodeExpiresAt = DateTime.UtcNow.AddMinutes(10),
                Usado = false,
                FechaCreacion = DateTime.UtcNow,
                AttemptCount = 0,
            };

            _context.VerificacionEmail.Add(verificacion);
            await _context.SaveChangesAsync();

            // Preparar email con template profesional
            var frontendUrl = _config["Frontend:Url"];
            try
            {
                var htmlTemplate = await _emailSender.GetVerificationEmailTemplateAsync(
                    usuario.Nombre,
                    code,
                    frontendUrl
                );

                await _emailSender.SendEmailAsync(
                    usuario.Email!,
                    "Código de verificación - Forest Barber",
                    htmlTemplate
                );
                _logger.LogInformation("Código de verificación enviado a {Email}", usuario.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error enviando código de verificación a {Email}",
                    usuario.Email
                );
            }

            return CreatedAtAction(
                nameof(PostRegistro),
                new { id = usuario.Id },
                new
                {
                    success = true,
                    message = "Usuario registrado. Revisa tu email para el código de verificación.",
                    usuario = new
                    {
                        usuario.Id,
                        usuario.Nombre,
                        usuario.Email,
                    },
                }
            );
        }

        // ========================================
        // Endpoints de Verificación
        // ========================================

        /// <summary>
        /// Verifica un código de 4 dígitos enviado al email del usuario.
        /// POST /api/public/Autenticacion/Verificar
        /// Acepta: {"Code":"2469"} o {"token":"2469"}
        /// </summary>
        [HttpPost("Verificar")]
        [AllowAnonymous]
        public async Task<IActionResult> PostVerificar([FromBody] VerificarRequest req)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var codigo = req?.Code ?? "";

            (bool success, string? error, int? usuarioId) result;

            // Si hay JWT, usar el usuarioId del token (sin pedir email)
            if (User.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst("userId")?.Value;
                if (int.TryParse(userIdClaim, out int usuarioId))
                {
                    result = await _verificacionService.VerifyCodeAsync(codigo, usuarioId);
                }
                else
                {
                    return Unauthorized(new { success = false, error = "Token inválido." });
                }
            }
            // Sin JWT, usar el email del request (durante registro)
            else
            {
                if (string.IsNullOrEmpty(req?.Email))
                {
                    return BadRequest(
                        new { success = false, error = "Email requerido si no hay autenticación." }
                    );
                }

                result = await _verificacionService.VerifyCodeAsync(codigo, req!.Email);
            }

            var (success, error, usuarioId2) = result;

            if (!success)
            {
                return BadRequest(
                    new { success = false, error = error ?? "No se pudo verificar el código." }
                );
            }

            // 🎉 Enviar email de bienvenida en background tras verificación exitosa
            if (usuarioId2.HasValue)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _verificacionService.SendWelcomeEmailAsync(
                            usuarioId2.Value,
                            _emailSender,
                            _config
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Error enviando email de bienvenida tras verificación"
                        );
                    }
                });
            }

            return Ok(
                new
                {
                    success = true,
                    message = "Cuenta verificada correctamente.",
                    usuarioId = usuarioId2,
                }
            );
        }

        /// <summary>
        /// Reenvía un código de verificación si el anterior expiró.
        /// POST /api/public/Autenticacion/ReenviarCodigo
        /// </summary>
        [HttpPost("ReenviarCodigo")]
        [AllowAnonymous]
        public async Task<IActionResult> ReenviarCodigo([FromBody] ReenviarCodigoRequest req)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Buscar usuario por email
            var usuario = await _context.Usuario.FirstOrDefaultAsync(u => u.Email == req.Email);
            if (usuario == null)
            {
                // Por seguridad, no revelar si el email existe o no
                return Ok(
                    new
                    {
                        success = true,
                        message = "Si el email está registrado, recibirá un nuevo código en breve.",
                    }
                );
            }

            // Si ya está verificado, no permitir
            if (usuario.Activo)
            {
                return BadRequest(
                    new { success = false, error = "Este usuario ya está verificado." }
                );
            }

            // Generar y enviar nuevo código
            var (success, error, usuarioId) =
                await _verificacionService.GenerateAndSendNewCodeAsync(
                    usuario.Id,
                    _emailSender,
                    _config,
                    _logger
                );

            if (!success)
            {
                return BadRequest(
                    new { success = false, error = error ?? "No se pudo enviar el código." }
                );
            }

            return Ok(
                new
                {
                    success = true,
                    message = "Se ha enviado un nuevo código de verificación a tu email. Revisa tu bandeja de entrada.",
                }
            );
        }

        /// <summary>
        /// Endpoint administrativo para limpiar manualmente registros expirados.
        /// POST /api/public/Autenticacion/LimpiarVerificaciones
        /// Solo accesible para administradores autenticados.
        /// </summary>
        [HttpPost("LimpiarVerificaciones")]
        [Authorize]
        public async Task<IActionResult> LimpiarVerificaciones([FromQuery] int daysRetention = 7)
        {
            if (daysRetention < 1 || daysRetention > 90)
            {
                return BadRequest(
                    new { success = false, error = "daysRetention debe estar entre 1 y 90 días." }
                );
            }

            var (registrosLimpiados, usuariosEliminados) =
                await _verificacionService.CleanupExpiredVerificationsAsync(daysRetention);

            return Ok(
                new
                {
                    success = true,
                    message = "Limpieza completada.",
                    registrosLimpiados = registrosLimpiados,
                    usuariosEliminados = usuariosEliminados,
                }
            );
        }

        // ========================================
        // Endpoints de Reseteo de Contraseña
        // ========================================

        /// <summary>
        /// Solicita reseteo de contraseña (envía token por email).
        /// POST /api/public/Autenticacion/OlvideContrasena
        /// </summary>
        [HttpPost("OlvideContrasena")]
        [AllowAnonymous]
        public async Task<IActionResult> OlvideContrasena([FromBody] OlvideContrasenaDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var (success, error) =
                await _verificacionService.GenerateAndSendPasswordResetTokenAsync(
                    dto.Email,
                    _emailSender,
                    _config,
                    _logger
                );

            // Por seguridad, siempre devolvemos éxito
            return Ok(
                new
                {
                    success = true,
                    message = "Si el email existe, recibirás instrucciones para resetear tu contraseña.",
                }
            );
        }

        /// <summary>
        /// Resetea la contraseña con token válido.
        /// POST /api/public/Autenticacion/ResetearContrasena
        /// </summary>
        [HttpPost("ResetearContrasena")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetearContrasena([FromBody] ResetearContrasenaDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var (success, error) = await _verificacionService.ResetPasswordAsync(
                dto.Token,
                dto.NuevaContrasena
            );

            if (!success)
            {
                return BadRequest(
                    new { success = false, error = error ?? "No se pudo resetear la contraseña." }
                );
            }

            return Ok(new { success = true, message = "Contraseña actualizada correctamente." });
        }

        /// <summary>
        /// Activación manual de usuario por un admin.
        /// Marca usuario.Activo = true y todas sus verificacion_email Usado = 1 para preservar auditoría.
        /// POST /api/public/Autenticacion/ActivarUsuarioManual
        /// </summary>
        [HttpPost("ActivarUsuarioManual")]
        [Authorize]
        public async Task<IActionResult> ActivarUsuarioManual(
            [FromBody] backend.Dtos.ManualActivateDto dto
        )
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var usuario = await _context.Usuario.FindAsync(dto.UsuarioId);
            if (usuario == null)
                return NotFound(new { success = false, error = "Usuario no encontrado." });

            if (usuario.Activo)
            {
                return BadRequest(new { success = false, error = "El usuario ya está activo." });
            }

            // Activar usuario
            usuario.Activo = true;

            // Marcar todas las verificaciones de este usuario que aún no están usadas (Usado = 0) como usadas (1)
            var verificacionesNoUsadas = await _context
                .VerificacionEmail.Where(v => v.UsuarioId == usuario.Id && !v.Usado)
                .ToListAsync();

            foreach (var v in verificacionesNoUsadas)
            {
                v.Usado = true;
            }

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation(
                    "Usuario {UsuarioId} activado manualmente por admin. Verificaciones marcadas: {Count}.",
                    usuario.Id,
                    verificacionesNoUsadas.Count
                );

                // 🎉 Enviar email de bienvenida tras activación manual
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _verificacionService.SendWelcomeEmailAsync(
                            usuario.Id,
                            _emailSender,
                            _config
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Error enviando email de bienvenida tras activación manual"
                        );
                    }
                });

                return Ok(
                    new
                    {
                        success = true,
                        message = "Usuario activado correctamente y verificaciones marcadas como usadas.",
                        usuarioId = usuario.Id,
                        verificacionesMarcadas = verificacionesNoUsadas.Count,
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error activando manualmente usuario {UsuarioId}", usuario.Id);
                return StatusCode(
                    500,
                    new { success = false, error = "Error interno al activar usuario." }
                );
            }
        }
    }
}
