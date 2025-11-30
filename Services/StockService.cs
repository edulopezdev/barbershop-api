using System;
using System.Linq;
using System.Threading.Tasks;
using backend.Data;
using backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace backend.Services
{
    public class StockService : IStockService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<StockService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IEmailSender _emailSender;

        public StockService(
            ApplicationDbContext context,
            ILogger<StockService> logger,
            IConfiguration configuration,
            IEmailSender emailSender
        )
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
            _emailSender = emailSender;
        }

        // =========================
        // Operaciones de stock
        // =========================

        public async Task<bool> UpdateStockAsync(int productoServicioId, int cantidad)
        {
            if (cantidad < 0)
                return false;

            var producto = await _context.ProductosServicios.FindAsync(productoServicioId);
            if (producto == null)
                return false;
            if (!producto.EsAlmacenable)
                return false;

            var actual = producto.Cantidad ?? 0;
            if (actual < cantidad)
                return false;

            producto.Cantidad = actual - cantidad;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task DevolverStockAsync(int productoServicioId, int cantidad)
        {
            if (cantidad <= 0)
                return;

            var producto = await _context.ProductosServicios.FindAsync(productoServicioId);
            if (producto == null)
                return;
            if (!producto.EsAlmacenable)
                return;

            var actual = producto.Cantidad ?? 0;
            producto.Cantidad = actual + cantidad;
            await _context.SaveChangesAsync();
        }

        public async Task<int> GetStockAsync(int productoServicioId)
        {
            var producto = await _context.ProductosServicios.FindAsync(productoServicioId);
            if (producto == null)
                return 0;
            return producto.Cantidad ?? 0;
        }

        private async Task<bool> IsStockLowAsync(int productoServicioId)
        {
            var lowStockThresholdString = _configuration["StockSettings:LowStockThreshold"] ?? "5";
            if (!int.TryParse(lowStockThresholdString, out int lowStockThreshold))
                lowStockThreshold = 5;

            var cantidad = await GetStockAsync(productoServicioId);
            return cantidad <= lowStockThreshold;
        }

        // =========================
        // Envíos de email relacionados al stock
        // =========================

        public async Task SendLowStockSummaryEmailAsync()
        {
            var lowStockThresholdString = _configuration["StockSettings:LowStockThreshold"] ?? "5";
            if (!int.TryParse(lowStockThresholdString, out int lowStockThreshold))
                lowStockThreshold = 5;

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

            var sender =
                _configuration["EmailSettings:SenderEmail"] ?? _configuration["EmailSettings:From"];
            var recipient =
                _configuration["EmailSettings:RecipientEmail"]
                ?? _configuration["EmailSettings:Recipient"]
                ?? sender;

            try
            {
                await _emailSender.SendEmailAsync(
                    recipient ?? (sender ?? "no-reply@example.com"),
                    "Alerta de productos con bajo stock",
                    body
                );

                _logger.LogInformation("Resumen de productos con bajo stock enviado por mail.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando mail de resumen de productos con bajo stock.");
            }
        }

        public async Task SendLowStockAlertAsync(int productoServicioId)
        {
            var producto = await _context.ProductosServicios.FindAsync(productoServicioId);
            if (producto == null)
            {
                _logger.LogError($"ProductoServicioId {productoServicioId} not found.");
                return;
            }

            if (!producto.EsAlmacenable)
            {
                _logger.LogInformation(
                    $"No se envía alerta de stock para servicios o productos no almacenables. ProductoServicioId: {productoServicioId}"
                );
                return;
            }

            if (!await IsStockLowAsync(productoServicioId))
            {
                _logger.LogInformation(
                    $"Stock suficiente para ProductoServicioId {productoServicioId}, no se envía alerta."
                );
                return;
            }

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

            var sender =
                _configuration["EmailSettings:SenderEmail"] ?? _configuration["EmailSettings:From"];
            var recipient =
                _configuration["EmailSettings:RecipientEmail"]
                ?? _configuration["EmailSettings:Recipient"]
                ?? sender;
            var subject = $"Alerta de stock bajo para el producto {producto.Nombre}";
            var body =
                $"El stock del producto {producto.Nombre} es menor o igual al límite inferior. Stock actual: {producto.Cantidad ?? 0}";

            try
            {
                await _emailSender.SendEmailAsync(
                    recipient ?? (sender ?? "no-reply@example.com"),
                    subject,
                    body
                );
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
