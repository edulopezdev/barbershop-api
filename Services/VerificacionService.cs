using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using backend.Data;
using backend.Models;
using backend.Services.Interfaces;
using BCrypt.Net; // ✅ Agregar BCrypt
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace backend.Services
{
    public class VerificacionService : IVerificacionService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<VerificacionService> _logger;

        public VerificacionService(
            ApplicationDbContext context,
            ILogger<VerificacionService> logger
        )
        {
            _context = context;
            _logger = logger;
        }

        public async Task<(bool Success, string? Error, int? UsuarioId)> VerifyCodeAsync(
            string code,
            string email
        )
        {
            // Validación: código no vacío
            if (string.IsNullOrWhiteSpace(code))
            {
                return (false, "Código requerido.", null);
            }

            // Validación: email no vacío
            if (string.IsNullOrWhiteSpace(email))
            {
                return (false, "Email requerido.", null);
            }

            // Remover espacios en blanco
            code = code.Trim();
            email = email.Trim().ToLower();

            // Validar que sea un código de 4 dígitos
            if (!int.TryParse(code, out var codeInt) || code.Length != 4)
            {
                return (false, "El código debe contener 4 dígitos.", null);
            }

            // Obtener usuario primero
            var usuario = await _context.Usuario.FirstOrDefaultAsync(u => u.Email == email);
            if (usuario == null)
            {
                return (false, "Usuario no encontrado.", null);
            }

            // Hashear el código enviado
            byte[] codeHash;
            using (var sha = SHA256.Create())
            {
                codeHash = sha.ComputeHash(Encoding.UTF8.GetBytes(code));
            }

            // Buscar el registro de verificación con este código Y de este usuario específico
            var verificacion = await _context.VerificacionEmail.FirstOrDefaultAsync(v =>
                v.CodeHash == codeHash && !v.Usado && v.UsuarioId == usuario.Id
            );

            if (verificacion == null)
            {
                // ❌ Código inválido para ESTE usuario: incrementar SU intento
                var codigoActuoDelUsuario = await _context
                    .VerificacionEmail.Where(v =>
                        v.UsuarioId == usuario.Id
                        && !v.Usado
                        && (v.CodeExpiresAt == null || v.CodeExpiresAt > DateTime.UtcNow)
                    )
                    .OrderByDescending(v => v.FechaCreacion)
                    .FirstOrDefaultAsync();

                if (codigoActuoDelUsuario != null)
                {
                    // Validar si ya alcanzó el límite de intentos ANTES de incrementar
                    const int maxAttempts = 3;
                    if (codigoActuoDelUsuario.AttemptCount >= maxAttempts)
                    {
                        return (
                            false,
                            "Demasiados intentos fallidos. Solicita un nuevo código.",
                            null
                        );
                    }

                    // Incrementar intento fallido SOLO del usuario que lo intentó
                    await _context.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE verificacion_email SET AttemptCount = AttemptCount + 1 WHERE Id = {codigoActuoDelUsuario.Id}"
                    );
                }

                return (false, "Código inválido o no encontrado.", null);
            }

            // Validar expiración del código (CodeExpiresAt) ANTES de contar intentos
            if (verificacion.CodeExpiresAt.HasValue && verificacion.CodeExpiresAt < DateTime.UtcNow)
            {
                // Código expirado: incrementar intento fallido
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE verificacion_email SET AttemptCount = AttemptCount + 1 WHERE Id = {verificacion.Id}"
                );
                return (false, "El código ha expirado. Solicita uno nuevo.", null);
            }

            // Validar intentos fallidos (máximo 3 intentos) - SOLO si no expiró
            const int maxAttempts_Valid = 3;
            if (verificacion.AttemptCount >= maxAttempts_Valid)
            {
                return (false, "Demasiados intentos fallidos. Solicita un nuevo código.", null);
            }

            // Marcar como usado de forma atómica E incrementar intentos
            var filasAfectadas = await _context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE verificacion_email SET Usado = 1, AttemptCount = AttemptCount + 1 WHERE Id = {verificacion.Id} AND Usado = 0"
            );

            if (filasAfectadas == 0)
            {
                // Otro proceso ya consumió el código
                return (false, "El código ya ha sido utilizado.", null);
            }

            // Activar usuario
            usuario.Activo = true;
            verificacion.Usado = true;
            verificacion.AttemptCount++;

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation(
                    "Usuario {UsuarioId} activado correctamente con código.",
                    usuario.Id
                );

                // 🎉 Enviar email de bienvenida después de verificación exitosa
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Necesitamos acceso a IEmailSender y IConfiguration aquí
                        // Por ahora, registramos que se debe enviar
                        await Task.Delay(1); // ✅ Agregar await para evitar warning
                        _logger.LogInformation(
                            "Usuario {UsuarioId} verificado - pendiente email de bienvenida",
                            usuario.Id
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Error enviando email de bienvenida a usuario {UsuarioId}",
                            usuario.Id
                        );
                    }
                });

                return (true, null, usuario.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error al activar usuario {UsuarioId} con código.",
                    usuario.Id
                );
                return (false, "Error interno al validar código.", null);
            }
        }

        /// <summary>
        /// Genera un nuevo código de 4 dígitos, invalida los anteriores y envía email.
        /// Devuelve (Success, ErrorMessage, UsuarioId).
        /// </summary>
        public async Task<(
            bool Success,
            string? Error,
            int? UsuarioId
        )> GenerateAndSendNewCodeAsync(
            int usuarioId,
            IEmailSender emailSender,
            IConfiguration config,
            ILogger logger
        )
        {
            var usuario = await _context.Usuario.FindAsync(usuarioId);
            if (usuario == null)
            {
                return (false, "Usuario no encontrado.", null);
            }

            if (usuario.Activo)
            {
                return (false, "El usuario ya está verificado.", null);
            }

            // Marcar códigos anteriores como "usados" para invalidarlos
            var codigosAnteriores = await _context
                .VerificacionEmail.Where(v => v.UsuarioId == usuarioId && !v.Usado)
                .ToListAsync();

            foreach (var codigo in codigosAnteriores)
            {
                codigo.Usado = true;
            }

            // Generar nuevo código de 4 dígitos
            int codeInt = RandomNumberGenerator.GetInt32(0, 10_000);
            string code = codeInt.ToString("D4");

            byte[] codeHash;
            using (var sha = SHA256.Create())
            {
                codeHash = sha.ComputeHash(Encoding.UTF8.GetBytes(code));
            }

            var verificacion = new VerificacionEmail
            {
                UsuarioId = usuarioId,
                CodeHash = codeHash,
                Expiracion = DateTime.UtcNow.AddHours(24),
                CodeExpiresAt = DateTime.UtcNow.AddMinutes(10),
                Usado = false,
                FechaCreacion = DateTime.UtcNow,
                AttemptCount = 0,
            };

            _context.VerificacionEmail.Add(verificacion);
            await _context.SaveChangesAsync();

            // Preparar y enviar email
            var frontendBase = config["Frontend:Url"] ?? "";
            var linkPart = string.Empty;
            if (!string.IsNullOrEmpty(frontendBase))
            {
                linkPart =
                    $"<p>Si tienes la app instalada, ábrela desde: <a href=\"{frontendBase}\">{frontendBase}</a></p>";
            }

            var html =
                $"<p>Hola {usuario.Nombre},</p>"
                + $"<p>Tu nuevo código de verificación es: <strong>{code}</strong></p>"
                + $"<p>El código expira en 10 minutos.</p>"
                + linkPart;

            try
            {
                var frontendUrl = config["Frontend:Url"];
                var htmlTemplate = await emailSender.GetVerificationEmailTemplateAsync(
                    usuario.Nombre ?? "Usuario", // ✅ Fix warning: usar fallback si es null
                    code,
                    frontendUrl
                );

                await emailSender.SendEmailAsync(
                    usuario.Email!,
                    "Nuevo código de verificación - Forest Barber",
                    htmlTemplate
                );

                logger.LogInformation(
                    "Nuevo código de verificación enviado a {Email}",
                    usuario.Email
                );
                return (true, null, usuarioId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error enviando nuevo código a {Email}", usuario.Email);
                return (false, "Error al enviar el código. Intenta más tarde.", null);
            }
        }

        /// <summary>
        /// Limpia registros de verificación expirados y usuarios no verificados después de N días.
        /// Política: Si un usuario no se verifica en 7 días, se elimina junto con sus registros de verificación.
        /// </summary>
        public async Task<(
            int RegistrosLimpiados,
            int UsuariosEliminados
        )> CleanupExpiredVerificationsAsync(int daysRetention = 7)
        {
            var registrosLimpiados = 0;
            var usuariosEliminados = 0;

            try
            {
                var fechaLimite = DateTime.UtcNow.AddDays(-daysRetention);

                // Tolerancia: admitir comparaciones con Utc y local por si los tiempos en BD no están normalizados.
                var ahoraUtc = DateTime.UtcNow;
                var ahoraLocal = DateTime.Now;

                // 1) Obtener verificaciones expiradas Y NO usadas (Usado = false)
                var verificacionesExpiradas = await _context
                    .VerificacionEmail.Where(v =>
                        !v.Usado
                        && v.CodeExpiresAt.HasValue
                        && (v.CodeExpiresAt <= ahoraUtc || v.CodeExpiresAt <= ahoraLocal)
                    )
                    .ToListAsync();

                registrosLimpiados = verificacionesExpiradas.Count;

                // IDs de usuarios afectados por esas verificaciones expiradas
                var usuarioIdsFromVerifs = verificacionesExpiradas
                    .Select(v => v.UsuarioId)
                    .Distinct()
                    .ToList();

                _logger.LogInformation(
                    "Verificaciones expiradas encontradas: {Count}. UsuarioIds afectados: {Ids}",
                    registrosLimpiados,
                    usuarioIdsFromVerifs
                );

                // 2) Buscar usuarios candidatos a eliminar:
                // - Activo = false
                // - RolId = 3
                // - asociados a las verificaciones expiradas (usuarioIdsFromVerifs)
                var usuariosCandidatos = await _context
                    .Usuario.Where(u =>
                        usuarioIdsFromVerifs.Contains(u.Id) && u.Activo == false && u.RolId == 3
                    )
                    .ToListAsync();

                // 3) Filtrar para NO borrar usuarios que tengan verificación usada (auditoría)
                var usuariosParaEliminar = new List<Usuario>();
                foreach (var u in usuariosCandidatos)
                {
                    var tieneVerificacionUsada = await _context.VerificacionEmail.AnyAsync(v =>
                        v.UsuarioId == u.Id && v.Usado == true
                    );

                    if (!tieneVerificacionUsada)
                    {
                        usuariosParaEliminar.Add(u);
                    }
                }

                usuariosEliminados = usuariosParaEliminar.Count;

                // 4) Eliminar primero las verificaciones expiradas (solo las NO usadas)
                if (verificacionesExpiradas.Count > 0)
                    _context.VerificacionEmail.RemoveRange(verificacionesExpiradas);

                // 5) Eliminar usuarios candidatos filtrados (solo los que NO tienen verificación usada)
                if (usuariosParaEliminar.Count > 0)
                    _context.Usuario.RemoveRange(usuariosParaEliminar);

                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Limpieza completada: {RegistrosLimpiados} verificaciones expiradas (no usadas) eliminadas, {UsuariosEliminados} usuarios no verificados eliminados. Se preservaron verificaciones con Usado = true para auditoría.",
                    registrosLimpiados,
                    usuariosEliminados
                );

                return (registrosLimpiados, usuariosEliminados);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error durante la limpieza de registros de verificación expirados."
                );
                return (0, 0);
            }
        }

        /// <summary>
        /// Verifica código obteniendo el email desde el JWT (para usuarios autenticados).
        /// Ideal para producción: el cliente no necesita enviar el email.
        /// </summary>
        public async Task<(bool Success, string? Error, int? UsuarioId)> VerifyCodeAsync(
            string code,
            int usuarioId
        )
        {
            // Validación: código no vacío
            if (string.IsNullOrWhiteSpace(code))
            {
                return (false, "Código requerido.", null);
            }

            // Remover espacios en blanco
            code = code.Trim();

            // Validar que sea un código de 4 dígitos
            if (!int.TryParse(code, out var codeInt) || code.Length != 4)
            {
                return (false, "El código debe contener 4 dígitos.", null);
            }

            // Obtener usuario por ID (desde el JWT)
            var usuario = await _context.Usuario.FindAsync(usuarioId);
            if (usuario == null)
            {
                return (false, "Usuario no encontrado.", null);
            }

            // Hashear el código enviado
            byte[] codeHash;
            using (var sha = SHA256.Create())
            {
                codeHash = sha.ComputeHash(Encoding.UTF8.GetBytes(code));
            }

            // Buscar el registro de verificación con este código Y de este usuario específico
            var verificacion = await _context.VerificacionEmail.FirstOrDefaultAsync(v =>
                v.CodeHash == codeHash && !v.Usado && v.UsuarioId == usuarioId
            );

            if (verificacion == null)
            {
                // ❌ Código inválido: incrementar intento del usuario actual
                var codigoActualDelUsuario = await _context
                    .VerificacionEmail.Where(v =>
                        v.UsuarioId == usuarioId
                        && !v.Usado
                        && (v.CodeExpiresAt == null || v.CodeExpiresAt > DateTime.UtcNow)
                    )
                    .OrderByDescending(v => v.FechaCreacion)
                    .FirstOrDefaultAsync();

                if (codigoActualDelUsuario != null)
                {
                    const int maxAttempts = 3;
                    if (codigoActualDelUsuario.AttemptCount >= maxAttempts)
                    {
                        return (
                            false,
                            "Demasiados intentos fallidos. Solicita un nuevo código.",
                            null
                        );
                    }

                    await _context.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE verificacion_email SET AttemptCount = AttemptCount + 1 WHERE Id = {codigoActualDelUsuario.Id}"
                    );
                }

                return (false, "Código inválido o no encontrado.", null);
            }

            // Validar expiración
            if (verificacion.CodeExpiresAt.HasValue && verificacion.CodeExpiresAt < DateTime.UtcNow)
            {
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE verificacion_email SET AttemptCount = AttemptCount + 1 WHERE Id = {verificacion.Id}"
                );
                return (false, "El código ha expirado. Solicita uno nuevo.", null);
            }

            // Validar intentos
            const int maxAttempts_Valid = 3;
            if (verificacion.AttemptCount >= maxAttempts_Valid)
            {
                return (false, "Demasiados intentos fallidos. Solicita un nuevo código.", null);
            }

            // Marcar como usado
            var filasAfectadas = await _context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE verificacion_email SET Usado = 1, AttemptCount = AttemptCount + 1 WHERE Id = {verificacion.Id} AND Usado = 0"
            );

            if (filasAfectadas == 0)
            {
                return (false, "El código ya ha sido utilizado.", null);
            }

            // Activar usuario
            usuario.Activo = true;
            verificacion.Usado = true;
            verificacion.AttemptCount++;

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation(
                    "Usuario {UsuarioId} activado correctamente con código.",
                    usuario.Id
                );

                // 🎉 Enviar email de bienvenida después de verificación exitosa
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(1); // ✅ Agregar await para evitar warning
                        _logger.LogInformation(
                            "Usuario {UsuarioId} verificado - pendiente email de bienvenida",
                            usuario.Id
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Error enviando email de bienvenida a usuario {UsuarioId}",
                            usuario.Id
                        );
                    }
                });

                return (true, null, usuario.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error al activar usuario {UsuarioId} con código.",
                    usuario.Id
                );
                return (false, "Error interno al validar código.", null);
            }
        }

        /// <summary>
        /// Genera código de 4 dígitos para reseteo de contraseña (válido 10 minutos) y envía por email.
        /// </summary>
        public async Task<(bool Success, string? Error)> GenerateAndSendPasswordResetTokenAsync(
            string email,
            IEmailSender emailSender,
            IConfiguration config,
            ILogger logger
        )
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return (false, "Email requerido.");
            }

            // Obtener usuario
            var usuario = await _context.Usuario.FirstOrDefaultAsync(u => u.Email == email);
            if (usuario == null)
            {
                // Por seguridad, no revelar si existe el usuario
                return (true, null);
            }

            // Generar código de 4 dígitos (como verificación de cuenta)
            int codeInt = RandomNumberGenerator.GetInt32(0, 10_000);
            string code = codeInt.ToString("D4");

            // Hashear código de 4 dígitos
            byte[] codeHash;
            using (var sha = SHA256.Create())
            {
                codeHash = sha.ComputeHash(Encoding.UTF8.GetBytes(code));
            }

            // Crear registro de reseteo (válido 10 minutos, como verificación)
            var reseteo = new ReseteoContrasena
            {
                UsuarioId = usuario.Id,
                TokenHash = codeHash, // Aquí guardamos el hash del código de 4 dígitos
                FechaCreacion = DateTime.UtcNow,
                FechaExpiracion = DateTime.UtcNow.AddMinutes(10), // ✅ 10 minutos como verificación
                Usado = false,
            };

            _context.ReseteoContrasena.Add(reseteo);
            await _context.SaveChangesAsync();

            // Enviar email con código de 4 dígitos (NO enlace)
            try
            {
                var htmlTemplate = await emailSender.GetPasswordResetTemplateAsync(
                    usuario.Nombre ?? "Usuario",
                    code // ✅ Pasar el código de 4 dígitos en lugar de enlace
                );

                await emailSender.SendEmailAsync(
                    usuario.Email!,
                    "Código para resetear contraseña - Forest Barber",
                    htmlTemplate
                );
                logger.LogInformation("Código de reset enviado a {Email}", usuario.Email);
                return (true, null);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error enviando código de reset a {Email}", usuario.Email);
                return (false, "Error al enviar email.");
            }
        }

        /// <summary>
        /// Valida código de 4 dígitos y resetea la contraseña.
        /// </summary>
        public async Task<(bool Success, string? Error)> ResetPasswordAsync(
            string token,
            string nuevaContrasena
        )
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return (false, "Código requerido.");
            }

            if (string.IsNullOrWhiteSpace(nuevaContrasena) || nuevaContrasena.Length < 8)
            {
                return (false, "Contraseña mínimo 8 caracteres.");
            }

            // Limpiar y validar que sea código de 4 dígitos
            token = token.Trim();
            if (!int.TryParse(token, out var codeInt) || token.Length != 4)
            {
                return (false, "El código debe contener 4 dígitos.");
            }

            // Hashear código recibido
            byte[] codeHash;
            using (var sha = SHA256.Create())
            {
                codeHash = sha.ComputeHash(Encoding.UTF8.GetBytes(token));
            }

            // Buscar registro de reseteo válido
            var reseteo = await _context.ReseteoContrasena.FirstOrDefaultAsync(r =>
                r.TokenHash == codeHash && !r.Usado && r.FechaExpiracion > DateTime.UtcNow
            );

            if (reseteo == null)
            {
                return (false, "Código inválido, usado o expirado.");
            }

            // Obtener usuario
            var usuario = await _context.Usuario.FindAsync(reseteo.UsuarioId);
            if (usuario == null)
            {
                return (false, "Usuario no encontrado.");
            }

            // ✅ BCrypt ya está siendo usado correctamente
            usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(nuevaContrasena);

            reseteo.Usado = true;

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation(
                    "Contraseña actualizada para usuario {UsuarioId}",
                    usuario.Id
                );
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error al resetear contraseña para usuario {UsuarioId}",
                    usuario.Id
                );
                return (false, "Error al resetear contraseña.");
            }
        }

        /// <summary>
        /// Envía email de bienvenida al usuario recién verificado
        /// </summary>
        public async Task SendWelcomeEmailAsync(
            int usuarioId,
            IEmailSender emailSender,
            IConfiguration config
        )
        {
            try
            {
                var usuario = await _context.Usuario.FindAsync(usuarioId);
                if (usuario == null || !usuario.Activo)
                {
                    _logger.LogWarning(
                        "No se puede enviar email de bienvenida - usuario {UsuarioId} no encontrado o inactivo",
                        usuarioId
                    );
                    return;
                }

                var frontendUrl = config["Frontend:Url"];
                var htmlTemplate = await emailSender.GetWelcomeEmailTemplateAsync(
                    usuario.Nombre ?? "Usuario",
                    frontendUrl
                );

                await emailSender.SendEmailAsync(
                    usuario.Email!,
                    "¡Bienvenido a Forest Barber! 🎉",
                    htmlTemplate
                );

                _logger.LogInformation("Email de bienvenida enviado a {Email}", usuario.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error enviando email de bienvenida a usuario {UsuarioId}",
                    usuarioId
                );
            }
        }
    }
}
