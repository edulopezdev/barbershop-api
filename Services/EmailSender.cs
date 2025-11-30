using System;
using System.IO;
using System.Threading.Tasks;
using backend.Services.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace backend.Services
{
    public class EmailSender : IEmailSender
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailSender> _logger;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public EmailSender(
            IConfiguration config,
            ILogger<EmailSender> logger,
            IWebHostEnvironment webHostEnvironment
        )
        {
            _config = config;
            _logger = logger;
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task SendEmailAsync(string to, string subject, string htmlBody)
        {
            if (string.IsNullOrWhiteSpace(to))
                throw new ArgumentException("Email destinatario requerido.", nameof(to));

            var message = new MimeMessage();

            var fromAddress =
                _config["EmailSettings:SenderEmail"]
                ?? _config["EmailSettings:From"]
                ?? "no-reply@example.com";
            var fromName = _config["EmailSettings:FromName"] ?? "Forest Barber Shop";

            message.From.Add(new MailboxAddress(fromName, fromAddress));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;

            // Usar solo TextPart con HTML (sin multipart ni attachments)
            message.Body = new TextPart("html") { Text = htmlBody };

            using var client = new SmtpClient();
            var host =
                _config["EmailSettings:SmtpHost"]
                ?? _config["EmailSettings:SmtpHostName"]
                ?? _config["EmailSettings:SmtpServer"]
                ?? "";
            var port = int.TryParse(_config["EmailSettings:SmtpPort"], out var p) ? p : 587;
            var useSsl = bool.TryParse(_config["EmailSettings:SmtpUseSsl"], out var s) ? s : true;

            var user =
                _config["EmailSettings:SmtpUser"]
                ?? _config["EmailSettings:SmtpUsername"]
                ?? _config["EmailSettings:SmtpUserName"];
            var pass = _config["EmailSettings:SmtpPassword"] ?? "";

            SecureSocketOptions socketOptions;
            if (useSsl)
            {
                socketOptions =
                    port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
            }
            else
            {
                socketOptions =
                    port == 25 ? SecureSocketOptions.None : SecureSocketOptions.StartTls;
            }

            try
            {
                await client.ConnectAsync(host, port, socketOptions);

                if (!string.IsNullOrEmpty(user))
                {
                    client.AuthenticationMechanisms.Remove("XOAUTH2");
                    await client.AuthenticateAsync(user, pass);
                }

                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation(
                    "Email HTML enviado a {To} via {Host}:{Port}",
                    to,
                    host,
                    port
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error enviando correo HTML a {To} via {Host}:{Port}",
                    to,
                    host,
                    port
                );
                throw;
            }
        }

        /// <summary>
        /// Carga y procesa template de verificación de email
        /// </summary>
        public async Task<string> GetVerificationEmailTemplateAsync(
            string nombreUsuario,
            string codigo,
            string? appLink = null
        )
        {
            var templatePath = Path.Combine(
                _webHostEnvironment.ContentRootPath,
                "Templates",
                "VerificationEmailTemplate.html"
            );

            if (!File.Exists(templatePath))
            {
                _logger.LogWarning(
                    "Template de verificación no encontrado en {Path}",
                    templatePath
                );
                // Fallback a template simple
                return GetFallbackVerificationTemplate(nombreUsuario, codigo, appLink);
            }

            var template = await File.ReadAllTextAsync(templatePath);
            var logoUrl = GetLogoUrl();

            var appLinkSection = "";
            if (!string.IsNullOrEmpty(appLink))
            {
                appLinkSection =
                    $@"
                <tr>
                    <td style=""font-size:14px; color:#1a73e8; text-align:center; padding-bottom: 15px;"">
                        <a href=""{appLink}"" style=""color:#1a73e8; text-decoration:none; font-weight:bold;"">
                            Abrir en la app Forest Barber
                        </a>
                    </td>
                </tr>";
            }

            return template
                .Replace("{{LogoUrl}}", logoUrl)
                .Replace("{{NombreUsuario}}", nombreUsuario)
                .Replace("{{Codigo}}", codigo)
                .Replace("{{AppLinkSection}}", appLinkSection);
        }

        /// <summary>
        /// Carga y procesa template de reset de contraseña con código de 4 dígitos
        /// </summary>
        public async Task<string> GetPasswordResetTemplateAsync(
            string nombreUsuario,
            string resetCode // ✅ Cambiar de resetLink a resetCode
        )
        {
            var templatePath = Path.Combine(
                _webHostEnvironment.ContentRootPath,
                "Templates",
                "PasswordResetEmailTemplate.html"
            );

            if (!File.Exists(templatePath))
            {
                _logger.LogWarning("Template de reset no encontrado en {Path}", templatePath);
                return GetFallbackResetTemplate(nombreUsuario, resetCode);
            }

            var template = await File.ReadAllTextAsync(templatePath);
            var logoUrl = GetLogoUrl();

            return template
                .Replace("{{LogoUrl}}", logoUrl)
                .Replace("{{NombreUsuario}}", nombreUsuario)
                .Replace("{{ResetCode}}", resetCode); // ✅ Usar código en lugar de link
        }

        /// <summary>
        /// Carga y procesa template de bienvenida
        /// </summary>
        public async Task<string> GetWelcomeEmailTemplateAsync(
            string nombreUsuario,
            string? appLink = null
        )
        {
            var templatePath = Path.Combine(
                _webHostEnvironment.ContentRootPath,
                "Templates",
                "WelcomeEmailTemplate.html"
            );

            if (!File.Exists(templatePath))
            {
                _logger.LogWarning("Template de bienvenida no encontrado en {Path}", templatePath);
                return GetFallbackWelcomeTemplate(nombreUsuario, appLink);
            }

            var template = await File.ReadAllTextAsync(templatePath);
            var logoUrl = GetLogoUrl();

            // Ya no usamos AppActionSection - template simplificado
            return template
                .Replace("{{LogoUrl}}", logoUrl)
                .Replace("{{NombreUsuario}}", nombreUsuario);
        }

        /// <summary>
        /// Carga y procesa template de turno confirmado
        /// </summary>
        public async Task<string> GetTurnoConfirmadoTemplateAsync(
            string nombreCliente,
            string fechaTurno,
            string horaTurno,
            string nombreBarbero,
            string precioEstimado
        )
        {
            var templatePath = Path.Combine(
                _webHostEnvironment.ContentRootPath,
                "Templates",
                "TurnoConfirmadoTemplate.html"
            );

            if (!File.Exists(templatePath))
            {
                _logger.LogWarning(
                    "Template de turno confirmado no encontrado en {Path}",
                    templatePath
                );
                return GetFallbackTurnoConfirmadoTemplate(
                    nombreCliente,
                    fechaTurno,
                    horaTurno,
                    nombreBarbero,
                    precioEstimado
                );
            }

            var template = await File.ReadAllTextAsync(templatePath);
            var logoUrl = GetLogoUrl();

            return template
                .Replace("{{LogoUrl}}", logoUrl)
                .Replace("{{NombreCliente}}", nombreCliente)
                .Replace("{{FechaTurno}}", fechaTurno)
                .Replace("{{HoraTurno}}", horaTurno)
                .Replace("{{NombreBarbero}}", nombreBarbero)
                .Replace("{{PrecioEstimado}}", precioEstimado);
        }

        /// <summary>
        /// Carga y procesa template de turno cancelado
        /// </summary>
        public async Task<string> GetTurnoCanceladoTemplateAsync(
            string nombreCliente,
            string fechaTurno,
            string horaTurno,
            string nombreBarbero
        )
        {
            var templatePath = Path.Combine(
                _webHostEnvironment.ContentRootPath,
                "Templates",
                "TurnoCanceladoTemplate.html"
            );

            if (!File.Exists(templatePath))
            {
                _logger.LogWarning(
                    "Template de turno cancelado no encontrado en {Path}",
                    templatePath
                );
                return GetFallbackTurnoCanceladoTemplate(
                    nombreCliente,
                    fechaTurno,
                    horaTurno,
                    nombreBarbero
                );
            }

            var template = await File.ReadAllTextAsync(templatePath);
            var logoUrl = GetLogoUrl();

            return template
                .Replace("{{LogoUrl}}", logoUrl)
                .Replace("{{NombreCliente}}", nombreCliente)
                .Replace("{{FechaTurno}}", fechaTurno)
                .Replace("{{HoraTurno}}", horaTurno)
                .Replace("{{NombreBarbero}}", nombreBarbero);
        }

        private string GetLogoUrl()
        {
            // Intentar cargar imagen como base64
            try
            {
                var logoPath = Path.Combine(
                    _webHostEnvironment.WebRootPath,
                    "images",
                    "LOGO_MAIL.png"
                );
                if (File.Exists(logoPath))
                {
                    var imageBytes = File.ReadAllBytes(logoPath);
                    var base64String = Convert.ToBase64String(imageBytes);
                    return $"data:image/png;base64,{base64String}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo cargar el logo como base64");
            }

            // Fallback a URL pública si está configurada
            var baseUrl = _config["BaseUrl"];
            if (!string.IsNullOrEmpty(baseUrl))
            {
                return $"{baseUrl}/images/LOGO_MAIL.png";
            }

            // Fallback final: imagen SVG generada
            return "data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iMTIwIiBoZWlnaHQ9IjgwIiB2aWV3Qm94PSIwIDAgMTIwIDgwIiBmaWxsPSJub25lIiB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciPgo8cmVjdCB3aWR0aD0iMTIwIiBoZWlnaHQ9IjgwIiBmaWxsPSIjMWE3M2U4Ii8+Cjx0ZXh0IHg9IjYwIiB5PSI0NSIgZm9udC1mYW1pbHk9IkFyaWFsLCBzYW5zLXNlcmlmIiBmb250LXNpemU9IjE0IiBmaWxsPSJ3aGl0ZSIgdGV4dC1hbmNob3I9Im1pZGRsZSI+Rm9yZXN0IEJhcmJlcjwvdGV4dD4KPHN2Zz4K";
        }

        private string GetFallbackVerificationTemplate(
            string nombreUsuario,
            string codigo,
            string? appLink
        )
        {
            var logoUrl = GetLogoUrl();
            return $@"
            <!DOCTYPE html>
            <html>
            <body style=""font-family: Arial, sans-serif; background-color:#f5f6f7; margin:0; padding:40px;"">
                <div style=""max-width:600px; margin:0 auto; background:#ffffff; padding:40px; border-radius:8px;"">
                    <div style=""text-align:center; margin-bottom:30px;"">
                        <img src=""{logoUrl}"" width=""120"" alt=""Forest Barber Logo"" />
                    </div>
                    <h2 style=""text-align:center; color:#333333;"">Código de verificación</h2>
                    <p style=""text-align:center; font-size:16px;"">Hola <strong>{nombreUsuario}</strong>,</p>
                    <p style=""text-align:center; font-size:16px;"">Tu código de verificación es:</p>
                    <div style=""text-align:center; margin:30px 0;"">
                        <span style=""background:#e9f4ff; border:2px solid #1a73e8; padding:15px 25px; 
                                     font-size:32px; font-weight:bold; color:#1a73e8; border-radius:8px;"">{codigo}</span>
                    </div>
                    <p style=""text-align:center; font-size:14px; color:#ff6b35;""><strong>Este código expira en 10 minutos</strong></p>
                    {(string.IsNullOrEmpty(appLink) ? "" : $@"<p style=""text-align:center;""><a href=""{appLink}"" style=""color:#1a73e8;"">Abrir en la app</a></p>")}
                    <p style=""text-align:center; font-size:14px; color:#777777; margin-top:40px; border-top:1px solid #eeeeee; padding-top:20px;"">
                        Si no solicitaste esto, podés ignorar este correo.
                    </p>
                </div>
            </body>
            </html>";
        }

        private string GetFallbackResetTemplate(string nombreUsuario, string resetCode)
        {
            var logoUrl = GetLogoUrl();
            return $@"
            <!DOCTYPE html>
            <html>
            <body style=""font-family: Arial, sans-serif; background-color:#f5f6f7; margin:0; padding:40px;"">
                <div style=""max-width:600px; margin:0 auto; background:#ffffff; padding:40px; border-radius:8px;"">
                    <div style=""text-align:center; margin-bottom:30px;"">
                        <img src=""{logoUrl}"" width=""120"" alt=""Forest Barber Logo"" />
                    </div>
                    <h2 style=""text-align:center; color:#333333;"">Código para resetear contraseña</h2>
                    <p style=""text-align:center; font-size:16px;"">Hola <strong>{nombreUsuario}</strong>,</p>
                    <p style=""text-align:center; font-size:16px;"">Usá el siguiente código para resetear tu contraseña:</p>
                    <div style=""text-align:center; margin:30px 0;"">
                        <span style=""background:#e9f4ff; border:2px solid #1a73e8; padding:15px 25px; 
                                     font-size:32px; font-weight:bold; color:#1a73e8; border-radius:8px;"">{resetCode}</span>
                    </div>
                    <p style=""text-align:center; font-size:14px; color:#ff6b35;""><strong>Este código expira en 10 minutos</strong></p>
                    <p style=""text-align:center; font-size:14px; color:#777777; margin-top:40px; border-top:1px solid #eeeeee; padding-top:20px;"">
                        Si no solicitaste esto, podés ignorar este correo con seguridad.
                    </p>
                </div>
            </body>
            </html>";
        }

        private string GetFallbackWelcomeTemplate(string nombreUsuario, string? appLink)
        {
            var logoUrl = GetLogoUrl();
            return $@"
            <!DOCTYPE html>
            <html>
            <body style=""font-family: Arial, sans-serif; background-color:#f5f6f7; margin:0; padding:40px;"">
                <div style=""max-width:600px; margin:0 auto; background:#ffffff; padding:40px; border-radius:8px;"">
                    <div style=""text-align:center; margin-bottom:30px;"">
                        <img src=""{logoUrl}"" width=""120"" alt=""Forest Barber Logo"" />
                    </div>
                    <h2 style=""text-align:center; color:#1a73e8; font-size:24px;"">¡Bienvenido a Forest Barber!</h2>
                    <p style=""text-align:center; font-size:18px; color:#333333;"">Hola <strong>{nombreUsuario}</strong>,</p>
                    <p style=""text-align:center; font-size:16px; color:#555555;"">
                        ¡Tu cuenta ha sido verificada exitosamente! 🎉<br/>
                        Ya podés empezar a disfrutar de nuestros servicios premium de barbería.
                    </p>
                    <div style=""margin:20px 0; padding:15px; background:#f8f9fa; border-radius:8px;"">
                        <p style=""font-weight:bold; margin:0 0 10px 0; color:#333333;"">¿Qué podés hacer ahora?</p>
                        <p style=""margin:0; color:#555555; line-height:1.6;"">
                            • 📅 Reservar turnos de forma rápida y sencilla<br/>
                            • ✂️ Elegir tu barbero favorito<br/>
                            • 📱 Gestionar tus citas desde la app
                        </p>
                    </div>
                    <p style=""text-align:center; font-size:14px; color:#777777; margin-top:30px; border-top:1px solid #eeeeee; padding-top:20px;"">
                        ¿Tenés alguna pregunta? Contactanos respondiendo a este email.
                    </p>
                </div>
            </body>
            </html>";
        }

        private string GetFallbackTurnoConfirmadoTemplate(
            string nombreCliente,
            string fechaTurno,
            string horaTurno,
            string nombreBarbero,
            string precioEstimado
        )
        {
            var logoUrl = GetLogoUrl();
            return $@"
            <!DOCTYPE html>
            <html>
            <body style=""font-family: Arial, sans-serif; background-color:#f5f6f7; margin:0; padding:40px;"">
                <div style=""max-width:600px; margin:0 auto; background:#ffffff; padding:40px; border-radius:8px;"">
                    <div style=""text-align:center; margin-bottom:30px;"">
                        <img src=""{logoUrl}"" width=""120"" alt=""Forest Barber Logo"" />
                    </div>
                    <h2 style=""text-align:center; color:#27ae60;"">✅ ¡Turno Confirmado!</h2>
                    <p style=""text-align:center; font-size:16px;"">Hola <strong>{nombreCliente}</strong>,</p>
                    <p style=""text-align:center; font-size:16px;"">Excelente noticia: tu turno ha sido confirmado.</p>
                    <div style=""background:#f8f9fa; padding:20px; border-radius:8px; margin:20px 0;"">
                        <p style=""font-weight:bold; margin:0 0 10px 0;"">📅 Detalles de tu turno:</p>
                        <p style=""margin:0; line-height:1.6;"">
                            <strong>📆 Fecha:</strong> {fechaTurno}<br/>
                            <strong>🕐 Hora:</strong> {horaTurno}<br/>
                            <strong>✂️ Barbero:</strong> {nombreBarbero}<br/>
                            <strong>💰 Precio estimado:</strong> {precioEstimado}
                        </p>
                    </div>
                    <p style=""text-align:center; font-size:14px; color:#e67e22; background:#fef9e7; padding:15px; border-radius:8px;"">
                        <strong>💡 Recordatorio:</strong> Te sugerimos llegar 5 minutos antes.
                    </p>
                    <p style=""text-align:center; font-size:14px; color:#777777; margin-top:30px; border-top:1px solid #eeeeee; padding-top:20px;"">
                        ¿Necesitás reprogramar? Respondé a este email.
                    </p>
                </div>
            </body>
            </html>";
        }

        private string GetFallbackTurnoCanceladoTemplate(
            string nombreCliente,
            string fechaTurno,
            string horaTurno,
            string nombreBarbero
        )
        {
            var logoUrl = GetLogoUrl();
            return $@"
            <!DOCTYPE html>
            <html>
            <body style=""font-family: Arial, sans-serif; background-color:#f5f6f7; margin:0; padding:40px;"">
                <div style=""max-width:600px; margin:0 auto; background:#ffffff; padding:40px; border-radius:8px;"">
                    <div style=""text-align:center; margin-bottom:30px;"">
                        <img src=""{logoUrl}"" width=""120"" alt=""Forest Barber Logo"" />
                    </div>
                    <h2 style=""text-align:center; color:#e74c3c;"">❌ Turno Cancelado</h2>
                    <p style=""text-align:center; font-size:16px;"">Hola <strong>{nombreCliente}</strong>,</p>
                    <p style=""text-align:center; font-size:16px;"">Lamentamos informarte que tu turno ha sido cancelado.</p>
                    <div style=""background:#fdf2f2; padding:20px; border-radius:8px; border-left:4px solid #e74c3c; margin:20px 0;"">
                        <p style=""font-weight:bold; margin:0 0 10px 0;"">📋 Turno cancelado:</p>
                        <p style=""margin:0; line-height:1.6;"">
                            <strong>📆 Fecha:</strong> {fechaTurno}<br/>
                            <strong>🕐 Hora:</strong> {horaTurno}<br/>
                            <strong>✂️ Barbero:</strong> {nombreBarbero}
                        </p>
                    </div>
                    <p style=""text-align:center; font-size:16px; background:#fff3cd; padding:15px; border-radius:8px;"">
                        <strong>🙏 Nuestras disculpas</strong><br/>
                        Sabemos que planificaste tu tiempo y lamentamos este inconveniente.
                    </p>
                    <p style=""text-align:center; font-size:14px; color:#777777; margin-top:30px; border-top:1px solid #eeeeee; padding-top:20px;"">
                        ¿Querés reprogramar? Respondé a este email. ¡Estamos para ayudarte! 😊
                    </p>
                </div>
            </body>
            </html>";
        }
    }
}
