using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using backend.Data;
using backend.Dtos;
using backend.Models;
using backend.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace backend.Services
{
    public class AtencionService : IAtencionService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AtencionService> _logger;
        private readonly IStockService _stockService;

        public AtencionService(
            ApplicationDbContext context,
            ILogger<AtencionService> logger,
            IStockService stockService
        )
        {
            _context = context;
            _logger = logger;
            _stockService = stockService;
        }

        public async Task<IActionResult> CrearAtencionAsync(CrearAtencionDto dto, int barberoId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var clienteExiste = await _context.Usuario.AnyAsync(u => u.Id == dto.ClienteId);
                if (!clienteExiste)
                {
                    return new BadRequestObjectResult(
                        new
                        {
                            status = 400,
                            error = "Bad Request",
                            message = $"ClienteId {dto.ClienteId} no existe.",
                        }
                    );
                }

                if (dto.Detalles == null || !dto.Detalles.Any())
                {
                    return new BadRequestObjectResult(
                        new
                        {
                            status = 400,
                            error = "Bad Request",
                            message = "La atención debe contener al menos un detalle.",
                        }
                    );
                }

                foreach (var detalle in dto.Detalles)
                {
                    var producto = await _context.ProductosServicios.FindAsync(
                        detalle.ProductoServicioId
                    );
                    if (producto == null)
                    {
                        return new BadRequestObjectResult(
                            new
                            {
                                status = 400,
                                error = "Bad Request",
                                message = $"ProductoServicioId {detalle.ProductoServicioId} no existe.",
                            }
                        );
                    }

                    if (producto.EsAlmacenable == true)
                    {
                        if (producto.Cantidad < detalle.Cantidad)
                        {
                            return new BadRequestObjectResult(
                                new
                                {
                                    status = 400,
                                    error = "Bad Request",
                                    message = $"No hay stock suficiente para el producto {producto.Nombre}. Stock disponible: {producto.Cantidad}.",
                                }
                            );
                        }
                        //producto.Cantidad -= detalle.Cantidad;
                        //_context.ProductosServicios.Update(producto);
                        await _stockService.UpdateStockAsync(
                            detalle.ProductoServicioId,
                            detalle.Cantidad
                        );
                    }
                }

                var atencion = new Atencion
                {
                    ClienteId = dto.ClienteId,
                    BarberoId = barberoId,
                    Fecha = dto.Fecha?.ToUniversalTime() ?? DateTime.UtcNow,
                    DetalleAtencion = dto
                        .Detalles.Select(d => new DetalleAtencion
                        {
                            ProductoServicioId = d.ProductoServicioId,
                            Cantidad = d.Cantidad,
                            PrecioUnitario = d.PrecioUnitario,
                            Observacion = d.Observacion,
                        })
                        .ToList(),
                };

                _context.Atencion.Add(atencion);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return new CreatedAtActionResult(
                    "GetAtencion",
                    "Atencion",
                    new { id = atencion.Id },
                    new
                    {
                        status = 201,
                        message = "Atención registrada correctamente.",
                        atencion,
                    }
                );
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error al guardar la atención");
                return new ObjectResult(
                    new
                    {
                        status = 500,
                        error = "Internal Server Error",
                        message = "Ocurrió un error al guardar la atención.",
                        details = ex.Message,
                    }
                )
                {
                    StatusCode = 500,
                };
            }
        }

        public async Task<IActionResult> ObtenerAtenciones(int page, int pageSize)
        {
            var totalAtenciones = await _context.Atencion.CountAsync();
            var atenciones = await _context
                .Atencion.Include(a => a.Cliente)
                .Include(a => a.Barbero)
                .Include(a => a.DetalleAtencion)
                .ThenInclude(d => d.ProductoServicio)
                .OrderBy(a => a.Fecha)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new OkObjectResult(
                new
                {
                    status = 200,
                    message = totalAtenciones > 0
                        ? "Lista de atenciones obtenida correctamente."
                        : "No hay atenciones disponibles.",
                    pagination = new
                    {
                        totalPages = (int)Math.Ceiling((double)totalAtenciones / pageSize),
                        currentPage = page,
                        pageSize,
                        totalAtenciones,
                    },
                    atenciones = atenciones ?? new List<Atencion>(),
                }
            );
        }

        public async Task<IActionResult> ObtenerAtencionPorId(int id)
        {
            var atencion = await _context
                .Atencion.Include(a => a.Cliente)
                .Include(a => a.Barbero)
                .Include(a => a.DetalleAtencion)
                .ThenInclude(d => d.ProductoServicio)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (atencion == null)
            {
                return new NotFoundObjectResult(
                    new
                    {
                        status = 404,
                        error = "Not Found",
                        message = "La atención no existe.",
                    }
                );
            }

            return new OkObjectResult(
                new
                {
                    status = 200,
                    message = "Atención encontrada.",
                    venta = new
                    {
                        atencionId = atencion.Id,
                        clienteId = atencion.ClienteId,
                        clienteNombre = atencion.Cliente?.Nombre,
                        fechaAtencion = atencion.Fecha.ToUniversalTime().ToString("o"),
                        detalles = atencion.DetalleAtencion.Select(d => new
                        {
                            productoServicioId = d.ProductoServicioId,
                            nombreProducto = d.ProductoServicio.Nombre,
                            cantidad = d.Cantidad,
                            precioUnitario = d.PrecioUnitario,
                            subtotal = d.Cantidad * d.PrecioUnitario,
                            observacion = d.Observacion,
                        }),
                        totalVenta = atencion.TotalCalculado,
                        pagos = new List<object>(),
                        montoPagado = 0,
                        estadoPago = "Sin pago",
                    },
                }
            );
        }

        public async Task<IActionResult> ActualizarAtencion(int id, ActualizarAtencionDto dto)
        {
            if (id != dto.Id)
            {
                return new BadRequestObjectResult(
                    new
                    {
                        status = 400,
                        error = "Bad Request",
                        message = "El ID en la URL no coincide con el ID del cuerpo de la solicitud.",
                    }
                );
            }

            var atencion = await _context.Atencion.FindAsync(id);
            if (atencion == null)
            {
                return new NotFoundObjectResult(
                    new
                    {
                        status = 404,
                        error = "Not Found",
                        message = "La atención no existe.",
                    }
                );
            }

            var clienteExiste = await _context.Usuario.AnyAsync(u => u.Id == dto.ClienteId);
            if (!clienteExiste)
            {
                return new BadRequestObjectResult(
                    new
                    {
                        status = 400,
                        error = "Bad Request",
                        message = "El ClienteId especificado no existe.",
                    }
                );
            }

            var barberoExiste = await _context.Usuario.AnyAsync(u =>
                u.Id == dto.BarberoId && u.RolId == 2
            );
            if (!barberoExiste)
            {
                return new BadRequestObjectResult(
                    new
                    {
                        status = 400,
                        error = "Bad Request",
                        message = "El BarberoId especificado no existe.",
                    }
                );
            }

            atencion.ClienteId = dto.ClienteId;
            atencion.BarberoId = dto.BarberoId;
            atencion.Fecha = dto.Fecha;

            // Sumar stock de los detalles eliminados
            if (dto.DetallesEliminados != null)
            {
                foreach (var detalleEliminado in dto.DetallesEliminados)
                {
                    await _stockService.UpdateStockAsync(
                        detalleEliminado.ProductoServicioId,
                        -detalleEliminado.Cantidad
                    ); // Cantidad negativa para sumar al stock
                }
            }

            _context.Entry(atencion).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Atencion.Any(e => e.Id == id))
                {
                    return new NotFoundObjectResult(
                        new
                        {
                            status = 404,
                            error = "Not Found",
                            message = "La atención no existe.",
                        }
                    );
                }
                throw;
            }

            return new OkObjectResult(
                new
                {
                    status = 200,
                    message = "Atención actualizada correctamente.",
                    atencion,
                }
            );
        }

        public async Task<IActionResult> EliminarAtencion(int id)
        {
            var atencion = await _context
                .Atencion.Include(a => a.DetalleAtencion)
                .ThenInclude(d => d.ProductoServicio)
                .FirstOrDefaultAsync(a => a.Id == id);
            if (atencion == null)
            {
                return new NotFoundObjectResult(
                    new
                    {
                        status = 404,
                        error = "Not Found",
                        message = "La atención no existe.",
                    }
                );
            }

            if (atencion.CierreDiarioId != null)
            {
                return new BadRequestObjectResult(
                    new
                    {
                        status = 400,
                        error = "Bad Request",
                        message = "No se puede eliminar la atención porque ya fue cerrada en caja.",
                        atencionId = id,
                    }
                );
            }

            // Devolver stock de los productos y servicios
            foreach (var detalle in atencion.DetalleAtencion)
            {
                await _stockService.UpdateStockAsync(detalle.ProductoServicioId, -detalle.Cantidad); // Cantidad negativa para sumar al stock
            }

            _context.Atencion.Remove(atencion);
            await _context.SaveChangesAsync();

            return new OkObjectResult(
                new
                {
                    status = 200,
                    message = "Atención eliminada correctamente.",
                    atencionIdEliminado = id,
                }
            );
        }

        public async Task<IActionResult> ObtenerResumenBarbero(int barberoId, int mes, int anio)
        {
            if (mes == 0 || anio == 0)
            {
                var fechaActual = DateTime.Now;
                mes = fechaActual.Month;
                anio = fechaActual.Year;
            }

            var query = _context.Atencion.Where(a =>
                a.BarberoId == barberoId && a.Fecha.Month == mes && a.Fecha.Year == anio
            );
            var atenciones = await query.ToListAsync();
            decimal totalIngresos = atenciones.Sum(a => a.TotalCalculado);
            int totalAtenciones = atenciones.Count;

            return new OkObjectResult(
                new
                {
                    status = 200,
                    data = new
                    {
                        totalAtenciones,
                        totalCortes = totalAtenciones,
                        totalServicios = 0,
                        totalIngresos,
                    },
                }
            );
        }
    }
}
