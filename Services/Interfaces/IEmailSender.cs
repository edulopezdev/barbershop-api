using System.Threading.Tasks;

namespace backend.Services.Interfaces
{
    public interface IEmailSender
    {
        Task SendEmailAsync(string to, string subject, string htmlBody);

        /// <summary>
        /// Obtiene template HTML para verificación de email
        /// </summary>
        Task<string> GetVerificationEmailTemplateAsync(
            string nombreUsuario,
            string codigo,
            string? appLink = null
        );

        /// <summary>
        /// Obtiene template HTML para reset de contraseña con código de 4 dígitos
        /// </summary>
        Task<string> GetPasswordResetTemplateAsync(string nombreUsuario, string resetCode);

        /// <summary>
        /// Obtiene template HTML de bienvenida después de verificación
        /// </summary>
        Task<string> GetWelcomeEmailTemplateAsync(string nombreUsuario, string? appLink = null);

        /// <summary>
        /// Obtiene template HTML para turno confirmado
        /// </summary>
        Task<string> GetTurnoConfirmadoTemplateAsync(
            string nombreCliente,
            string fechaTurno,
            string horaTurno,
            string nombreBarbero,
            string precioEstimado
        );

        /// <summary>
        /// Obtiene template HTML para turno cancelado
        /// </summary>
        Task<string> GetTurnoCanceladoTemplateAsync(
            string nombreCliente,
            string fechaTurno,
            string horaTurno,
            string nombreBarbero
        );
    }
}
