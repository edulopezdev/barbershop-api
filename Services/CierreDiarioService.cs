using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using backend.Data;
using backend.Models;
using backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace backend.Services
{
    public class CierreDiarioService : ICierreDiarioService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CierreDiarioService> _logger;
        private readonly IStockService _stockService;

        public CierreDiarioService(
            ApplicationDbContext context,
            IConfiguration configuration,
            ILogger<CierreDiarioService> logger,
            IStockService stockService
        )
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
            _stockService = stockService;
        }

        public async Task<CierreDiario> CrearCierreAsync(CierreDiario cierre)
        {
            cierre.FechaCierre = DateTime.UtcNow;

            // Guardar el cierre primero
            _context.CierresDiarios.Add(cierre);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                $"Cierre creado con ID: {cierre.Id} para la fecha {cierre.Fecha:yyyy-MM-dd}"
            );

            try
            {
                var atenciones = await _context
                    .Atencion.Where(a =>
                        a.Fecha.Date == cierre.Fecha.Date && a.CierreDiarioId == null
                    )
                    .ToListAsync();

                foreach (var atencion in atenciones)
                {
                    atencion.CierreDiarioId = cierre.Id;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    $"Atenciones actualizadas: {atenciones.Count} registros vinculados al cierre ID {cierre.Id}"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    $"Error al actualizar atenciones para el cierre ID {cierre.Id}"
                );
                throw;
            }

            // Enviar mail de bajo stock al cerrar la caja
            await _stockService.SendLowStockSummaryEmailAsync();
            return cierre;
        }

        public async Task<List<CierreDiario>> ObtenerCierresAsync()
        {
            return await _context.CierresDiarios.OrderByDescending(c => c.Fecha).ToListAsync();
        }

        public async Task<CierreDiario?> ObtenerCierrePorIdAsync(int id)
        {
            return await _context.CierresDiarios.FindAsync(id);
        }

        public async Task<CierreDiario?> ObtenerCierrePorFechaAsync(DateTime fecha)
        {
            return await _context
                .CierresDiarios.Include(c => c.Pagos)
                .FirstOrDefaultAsync(c => c.Fecha.Date == fecha.Date);
        }

        public async Task<bool> BloquearCierreAsync(int id)
        {
            var cierre = await _context.CierresDiarios.FindAsync(id);
            if (cierre == null)
                return false;

            cierre.Cerrado = true;
            _context.CierresDiarios.Update(cierre);
            await _context.SaveChangesAsync();

            // Enviar mail de bajo stock al cerrar la caja
            await _stockService.SendLowStockSummaryEmailAsync();

            return true;
        }

        public async Task<object> ObtenerResumenDelDiaAsync(DateTime fecha)
        {
            // 1. Si hay cierre registrado → retornar datos persistidos
            var cierreExistente = await _context
                .CierresDiarios.Include(c => c.Pagos)
                .FirstOrDefaultAsync(c => c.Fecha.Date == fecha.Date);

            if (cierreExistente != null)
            {
                return new
                {
                    fecha = cierreExistente.Fecha,
                    totalUnidadesProductos = 0, // Solo si guardaste unidades
                    totalMontoProductos = cierreExistente.TotalProductosVendidos,
                    totalUnidadesServicios = 0, // Solo si guardaste unidades
                    totalMontoServicios = cierreExistente.TotalServiciosVendidos,
                    totalIngresos = cierreExistente.TotalVentasDia,
                    pagos = cierreExistente.Pagos == null
                        ? new object[0]
                        : cierreExistente
                            .Pagos.Select(p => new
                            {
                                metodoPago = p.MetodoPagoNombre,
                                monto = p.Monto,
                            })
                            .ToArray(),
                };
            }

            // 2. Si NO hay cierre → construir resumen en tiempo real
            var productosQuery =
                from da in _context.DetalleAtencion
                join a in _context.Atencion on da.AtencionId equals a.Id
                join ps in _context.ProductosServicios on da.ProductoServicioId equals ps.Id
                where a.Fecha.Date == fecha.Date && ps.EsAlmacenable == true
                select new { da.Cantidad, da.PrecioUnitario };

            var serviciosQuery =
                from da in _context.DetalleAtencion
                join a in _context.Atencion on da.AtencionId equals a.Id
                join ps in _context.ProductosServicios on da.ProductoServicioId equals ps.Id
                where a.Fecha.Date == fecha.Date && ps.EsAlmacenable == false
                select new { da.Cantidad, da.PrecioUnitario };

            var pagosQuery =
                from p in _context.Pagos
                join a in _context.Atencion on p.AtencionId equals a.Id
                where a.Fecha.Date == fecha.Date
                group p by p.MetodoPago into g
                select new { metodoPago = g.Key, monto = g.Sum(p => p.Monto) };

            var productos = await productosQuery.ToListAsync();
            var servicios = await serviciosQuery.ToListAsync();
            var pagos = await pagosQuery.ToListAsync();

            var totalUnidadesProductos = productos.Sum(p => p.Cantidad);
            var totalMontoProductos = productos.Sum(p => p.Cantidad * p.PrecioUnitario);

            var totalUnidadesServicios = servicios.Sum(s => s.Cantidad);
            var totalMontoServicios = servicios.Sum(s => s.Cantidad * s.PrecioUnitario);

            var totalIngresos = pagos.Sum(p => p.monto);

            return new
            {
                fecha = fecha.Date,
                totalUnidadesProductos,
                totalMontoProductos,
                totalUnidadesServicios,
                totalMontoServicios,
                totalIngresos,
                pagos,
            };
        }

        public async Task<bool> ValidarPasswordAsync(int userId, string password)
        {
            var usuario = await _context.Usuario.FindAsync(userId);
            if (usuario == null || string.IsNullOrEmpty(usuario.PasswordHash))
                return false;

            return BCrypt.Net.BCrypt.Verify(password, usuario.PasswordHash);
        }

        public async Task<(bool existe, bool estaCerrada)> VerificarVentaCerradaAsync(
            int atencionId
        )
        {
            var atencion = await _context.Atencion.FindAsync(atencionId);
            if (atencion == null)
                return (false, false);

            return (true, atencion.CierreDiarioId != null);
        }
    }
}
