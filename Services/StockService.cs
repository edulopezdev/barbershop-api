using System;
using System.Threading.Tasks;
using backend.Data;
using backend.Services.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace backend.Services
{
    public class StockService : IStockService
    {
        /// <summary>
        /// Envía un solo mail con la lista de productos almacenables con bajo stock (solo si hay alguno).
        /// </summary>
        public async Task SendLowStockSummaryEmailAsync()
        {
            var lowStockThresholdString = _configuration["StockSettings:LowStockThreshold"] ?? "5";
            if (!int.TryParse(lowStockThresholdString, out int lowStockThreshold))
            {
                _logger.LogWarning(
                    "LowStockThreshold not configured correctly. Using default value of 5."
                );
                lowStockThreshold = 5;
            }

            var productosBajoStock = await _context
                .ProductosServicios.Where(p =>
                    p.EsAlmacenable && (p.Cantidad ?? 0) <= lowStockThreshold
                )
                .ToListAsync();

            if (productosBajoStock.Count == 0)
            {
                _logger.LogInformation(
                    "No hay productos con bajo stock. No se enviará mail de resumen."
                );
                return;
            }

            var body = "Los siguientes productos tienen stock bajo y requieren reposición:\n\n";
            foreach (var p in productosBajoStock)
            {
                body += $"- {p.Nombre} (Stock actual: {p.Cantidad ?? 0})\n";
            }

            var email = new MimeMessage();
            email.From.Add(MailboxAddress.Parse(_configuration["EmailSettings:SenderEmail"]));
            email.To.Add(MailboxAddress.Parse(_configuration["EmailSettings:RecipientEmail"]));
            email.Subject = "Alerta de productos con bajo stock";
            email.Body = new TextPart("plain") { Text = body };

            try
            {
                using var smtp = new SmtpClient();
                string? smtpPortString = _configuration["EmailSettings:SmtpPort"];
                if (
                    string.IsNullOrWhiteSpace(smtpPortString)
                    || !int.TryParse(smtpPortString, out int smtpPort)
                )
                {
                    _logger.LogError(
                        "Invalid or missing SmtpPort configuration. Email sending aborted."
                    );
                    return;
                }

                smtp.Connect(
                    _configuration["EmailSettings:SmtpServer"],
                    smtpPort,
                    SecureSocketOptions.StartTls
                );
                smtp.Authenticate(
                    _configuration["EmailSettings:SmtpUsername"],
                    _configuration["EmailSettings:SmtpPassword"]
                );
                smtp.Send(email);
                smtp.Disconnect(true);

                _logger.LogInformation("Resumen de productos con bajo stock enviado por mail.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando mail de resumen de productos con bajo stock.");
            }
        }

        private readonly ApplicationDbContext _context;
        private readonly ILogger<StockService> _logger;
        private readonly IConfiguration _configuration;

        public StockService(
            ApplicationDbContext context,
            ILogger<StockService> logger,
            IConfiguration configuration
        )
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// Descuenta stock de un producto. Lanza excepción si no hay suficiente stock.
        /// </summary>
        public async Task<bool> UpdateStockAsync(int productoServicioId, int cantidad)
        {
            var producto = await _context.ProductosServicios.FindAsync(productoServicioId);
            if (producto == null)
            {
                _logger.LogError($"ProductoServicioId {productoServicioId} not found.");
                throw new InvalidOperationException(
                    $"ProductoServicioId {productoServicioId} no encontrado."
                );
            }

            int stockActual = producto.Cantidad ?? 0;
            if (cantidad > stockActual)
            {
                _logger.LogWarning(
                    $"No hay stock suficiente para el producto {producto.Nombre}. Stock disponible: {stockActual}, solicitado: {cantidad}."
                );
                return false;
            }

            producto.Cantidad = stockActual - cantidad;
            _context.ProductosServicios.Update(producto);
            await _context.SaveChangesAsync();

            await SendLowStockAlertAsync(productoServicioId);
            return true;
        }

        /// <summary>
        /// Devuelve stock a un producto (por ejemplo, al eliminar o reducir una venta).
        /// </summary>
        public async Task DevolverStockAsync(int productoServicioId, int cantidad)
        {
            var producto = await _context.ProductosServicios.FindAsync(productoServicioId);
            if (producto == null)
            {
                _logger.LogError($"ProductoServicioId {productoServicioId} not found.");
                throw new InvalidOperationException(
                    $"ProductoServicioId {productoServicioId} no encontrado."
                );
            }

            producto.Cantidad = (producto.Cantidad ?? 0) + cantidad;
            _context.ProductosServicios.Update(producto);
            await _context.SaveChangesAsync();
        }

        public async Task<int> GetStockAsync(int productoServicioId)
        {
            var producto = await _context.ProductosServicios.FindAsync(productoServicioId);
            if (producto == null)
            {
                _logger.LogError($"ProductoServicioId {productoServicioId} not found.");
                return 0;
            }

            return producto.Cantidad ?? 0;
        }

        private async Task<bool> IsStockLowAsync(int productoServicioId)
        {
            var producto = await _context.ProductosServicios.FindAsync(productoServicioId);
            if (producto == null)
            {
                _logger.LogError($"ProductoServicioId {productoServicioId} not found.");
                return false;
            }

            var lowStockThresholdString = _configuration["StockSettings:LowStockThreshold"] ?? "5";
            if (!int.TryParse(lowStockThresholdString, out int lowStockThreshold))
            {
                _logger.LogWarning(
                    "LowStockThreshold not configured correctly. Using default value of 5."
                );
                lowStockThreshold = 5;
            }

            int cantidad = producto.Cantidad ?? 0;
            return cantidad <= lowStockThreshold;
        }

        public async Task SendLowStockAlertAsync(int productoServicioId)
        {
            var producto = await _context.ProductosServicios.FindAsync(productoServicioId);
            if (producto == null)
            {
                _logger.LogError($"ProductoServicioId {productoServicioId} not found.");
                return;
            }

            // Solo productos almacenables
            if (!producto.EsAlmacenable)
            {
                _logger.LogInformation(
                    $"No se envía alerta de stock para servicios o productos no almacenables. ProductoServicioId: {productoServicioId}"
                );
                return;
            }

            if (!await IsStockLowAsync(productoServicioId))
            {
                return;
            }

            // Verificar si la caja del día está cerrada
            var hoy = DateTime.Today;
            var cierreHoy = await _context.CierresDiarios.FirstOrDefaultAsync(c =>
                c.FechaCierre.Date == hoy && c.Cerrado
            );
            if (cierreHoy == null)
            {
                _logger.LogInformation(
                    $"No se envía alerta de stock porque la caja de hoy no está cerrada. ProductoServicioId: {productoServicioId}"
                );
                return;
            }

            var email = new MimeMessage();
            email.From.Add(MailboxAddress.Parse(_configuration["EmailSettings:SenderEmail"]));
            email.To.Add(MailboxAddress.Parse(_configuration["EmailSettings:RecipientEmail"]));
            email.Subject = $"Alerta de stock bajo para el producto {producto.Nombre}";
            email.Body = new TextPart("plain")
            {
                Text =
                    $"El stock del producto {producto.Nombre} es menor o igual al límite inferior.",
            };

            try
            {
                using var smtp = new SmtpClient();
                string? smtpPortString = _configuration["EmailSettings:SmtpPort"];
                if (
                    string.IsNullOrWhiteSpace(smtpPortString)
                    || !int.TryParse(smtpPortString, out int smtpPort)
                )
                {
                    _logger.LogError(
                        "Invalid or missing SmtpPort configuration. Email sending aborted."
                    );
                    return;
                }

                smtp.Connect(
                    _configuration["EmailSettings:SmtpServer"],
                    smtpPort,
                    SecureSocketOptions.StartTls
                );
                smtp.Authenticate(
                    _configuration["EmailSettings:SmtpUsername"],
                    _configuration["EmailSettings:SmtpPassword"]
                );
                smtp.Send(email);
                smtp.Disconnect(true);

                _logger.LogInformation(
                    $"Low stock alert email sent for ProductoServicioId {productoServicioId}."
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    $"Error sending low stock alert email for ProductoServicioId {productoServicioId}."
                );
            }
        }
    }
}
