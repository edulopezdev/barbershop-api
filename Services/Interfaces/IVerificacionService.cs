using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace backend.Services.Interfaces
{
    public interface IVerificacionService
    {
        Task<(bool Success, string? Error, int? UsuarioId)> VerifyCodeAsync(
            string code,
            string email
        );

        Task<(bool Success, string? Error, int? UsuarioId)> VerifyCodeAsync(
            string code,
            int usuarioId
        );

        /// <summary>
        /// Genera un nuevo código de 4 dígitos, invalida los anteriores y envía email.
        /// Devuelve (Success, ErrorMessage, UsuarioId).
        /// </summary>
        Task<(bool Success, string? Error, int? UsuarioId)> GenerateAndSendNewCodeAsync(
            int usuarioId,
            IEmailSender emailSender,
            IConfiguration config,
            ILogger logger
        );

        /// <summary>
        /// Limpia registros de verificación expirados y usuarios no verificados después de N días.
        /// Devuelve (RegistrosLimpiados, UsuariosEliminados).
        /// </summary>
        Task<(int RegistrosLimpiados, int UsuariosEliminados)> CleanupExpiredVerificationsAsync(
            int daysRetention = 7
        );

        /// <summary>
        /// Genera token de reseteo de contraseña (válido 1 hora) y envía por email.
        /// </summary>
        Task<(bool Success, string? Error)> GenerateAndSendPasswordResetTokenAsync(
            string email,
            IEmailSender emailSender,
            IConfiguration config,
            ILogger logger
        );

        /// <summary>
        /// Valida token y resetea la contraseña.
        /// </summary>
        Task<(bool Success, string? Error)> ResetPasswordAsync(
            string tokenHex,
            string nuevaContrasena
        );

        /// <summary>
        /// Envía email de bienvenida al usuario recién verificado
        /// </summary>
        Task SendWelcomeEmailAsync(int usuarioId, IEmailSender emailSender, IConfiguration config);
    }
}
