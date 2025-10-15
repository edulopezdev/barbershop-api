using System.Text.Json;
using backend.Data;
using backend.Dtos;
using backend.Models;
using backend.Services;
using backend.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DetalleAtencionController : ControllerBase
    {
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DetalleAtencionController> _logger;
    private readonly IVentaService _ventaService;
    private readonly IStockService _stockService;

        public DetalleAtencionController(
            ApplicationDbContext context,
            ILogger<DetalleAtencionController> logger,
            IVentaService ventaService,
            IStockService stockService
        )
        {
            _context = context;
            _logger = logger;
            _ventaService = ventaService;
            _stockService = stockService;
        }

        // GET: api/detalleatencion
        [HttpGet]
        public IActionResult GetDetallesAtencion(int page = 1, int pageSize = 10)
        {
            var totalDetalles = _context.DetalleAtencion.Count();
            var detalles = _context
                .DetalleAtencion.OrderBy(d => d.AtencionId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Ok(
                new
                {
                    status = 200,
                    message = totalDetalles > 0
                        ? "Lista de detalles de atención obtenida correctamente."
                        : "No hay detalles de atención disponibles.",
                    pagination = new
                    {
                        totalPages = (int)Math.Ceiling((double)totalDetalles / pageSize),
                        currentPage = page,
                        pageSize,
                        totalDetalles,
                    },
                    detalles = detalles ?? new List<DetalleAtencion>(),
                }
            );
        }

        // GET: api/detalleatencion/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetDetalleAtencion(int id)
        {
            var detalleAtencion = await _context.DetalleAtencion.FindAsync(id);
            if (detalleAtencion == null)
            {
                return NotFound(
                    new
                    {
                        status = 404,
                        error = "Not Found",
                        message = "El detalle de atención no existe.",
                    }
                );
            }

            return Ok(
                new
                {
                    status = 200,
                    message = "Detalle de atención encontrado.",
                    detalleAtencion,
                }
            );
        }

        // GET: api/detalleatencion/ventas
        [HttpGet("ventas")]
        public async Task<IActionResult> GetVentas(
            int page = 1,
            int pageSize = 10,
            string? clienteNombre = null,
            int? productoServicioId = null,
            string? productoNombre = null,
            DateTime? fecha = null,
            string ordenarPor = "fecha",
            string? ordenDescendente = null, // ahora es string para parsear manualmente
            decimal? montoMin = null,
            decimal? montoMax = null,
            string? estadoPago = null
        )
        {
            try
            {
                // Parseamos ordenDescendente con valor por defecto true
                bool ordenDesc = true;
                if (!string.IsNullOrEmpty(ordenDescendente))
                {
                    bool.TryParse(ordenDescendente, out ordenDesc);
                }

                DateTime? fechaDesde = null;
                DateTime? fechaHasta = null;

                if (fecha.HasValue)
                {
                    fechaDesde = fecha.Value.Date;
                    fechaHasta = fecha.Value.Date.AddDays(1).AddTicks(-1);
                }

                var resultado = await _ventaService.ObtenerVentasAsync(
                    page,
                    pageSize,
                    clienteNombre,
                    productoServicioId,
                    productoNombre,
                    fechaDesde,
                    fechaHasta,
                    ordenarPor,
                    ordenDesc,
                    montoMin,
                    montoMax,
                    estadoPago
                );

                return Ok(resultado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en GetVentas");
                return StatusCode(
                    500,
                    new
                    {
                        status = 500,
                        error = "Internal Server Error",
                        message = "Ocurrió un error al obtener las ventas.",
                        details = ex.Message,
                    }
                );
            }
        }

        // GET: api/detalleatencion/ventas/{id}
        [HttpGet("ventas/{id}")]
        public async Task<IActionResult> GetVentaById(int id)
        {
            var atencion = await _context
                .Atencion.Include(a => a.Cliente)
                .Include(a => a.DetalleAtencion)
                .ThenInclude(d => d.ProductoServicio)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (atencion == null)
            {
                return NotFound(
                    new
                    {
                        status = 404,
                        error = "Not Found",
                        message = "La atención no existe.",
                    }
                );
            }

            var pagos = await _context.Pagos.Where(p => p.AtencionId == id).ToListAsync();

            var detalles = atencion
                .DetalleAtencion.Select(d => new DetalleVentaDto
                {
                    ProductoServicioId = d.ProductoServicioId,
                    NombreProducto = d.ProductoServicio?.Nombre ?? "Producto/Servicio borrado",
                    Cantidad = d.Cantidad,
                    PrecioUnitario = d.PrecioUnitario,
                    Subtotal = d.Cantidad * d.PrecioUnitario,
                    Observacion = d.Observacion,
                    EsAlmacenable = d.ProductoServicio != null ? d.ProductoServicio.EsAlmacenable : false,
                })
                .ToList();

            _logger.LogInformation(
                "DetalleAtencion enviados para la venta con Id {AtencionId}: {DetallesJson}",
                id,
                JsonSerializer.Serialize(detalles)
            );

            var venta = new VentaDto
            {
                AtencionId = atencion.Id,
                ClienteId = atencion.ClienteId,
                ClienteNombre = atencion.Cliente?.Nombre ?? "Cliente Desconocido",
                Cliente = new UsuarioResumenDto
                {
                    Id = atencion.Cliente?.Id ?? 0,
                    Nombre = atencion.Cliente?.Nombre ?? "Cliente Desconocido",
                },
                FechaAtencion = atencion.Fecha,
                Detalles = detalles,
                Pagos = pagos
                    .Select(p => new PagoInfoDto
                    {
                        PagoId = p.Id,
                        MetodoPago = p.MetodoPago.ToString(),
                        Monto = p.Monto,
                        FechaPago = p.Fecha,
                    })
                    .ToList(),
            };

            return Ok(
                new
                {
                    status = 200,
                    message = "Venta encontrada.",
                    venta,
                }
            );
        }

        // POST: api/detalleatencion/actualizar/{id}
        [HttpPost("actualizar/{id}")]
        public async Task<IActionResult> ActualizarDetallesVenta(
            int id,
            [FromBody] ActualizarDetallesDto dto
        )
        {
            _logger.LogInformation("Actualizando detalles de venta con ID: {Id}", id);

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var atencion = await _context
                    .Atencion.Include(a => a.DetalleAtencion)
                    .ThenInclude(d => d.ProductoServicio)
                    .FirstOrDefaultAsync(a => a.Id == id);

                if (atencion == null)
                {
                    return NotFound(
                        new
                        {
                            status = 404,
                            error = "Not Found",
                            message = "La venta no existe.",
                        }
                    );
                }

                if (atencion.CierreDiarioId != null)
                {
                    return BadRequest(
                        new
                        {
                            status = 400,
                            error = "Bad Request",
                            message = "No se puede modificar una venta que ya está cerrada.",
                        }
                    );
                }

                var detallesOriginales = atencion.DetalleAtencion.ToDictionary(d =>
                    d.ProductoServicioId
                );
                var detallesNuevos = dto
                    .Detalles.GroupBy(d => d.ProductoServicioId)
                    .ToDictionary(
                        g => g.Key,
                        g => new DetalleAtencionDto
                        {
                            ProductoServicioId = g.Key,
                            Cantidad = g.Sum(d => d.Cantidad),
                            PrecioUnitario = g.First().PrecioUnitario, // Asumimos que el precio es el mismo
                            Observacion = g.First().Observacion,
                        }
                    );

                // Devolver stock de productos eliminados o reducidos
                foreach (var original in detallesOriginales.Values)
                {
                    if (original.ProductoServicio.EsAlmacenable == true)
                    {
                        int nuevaCantidad = detallesNuevos.ContainsKey(original.ProductoServicioId)
                            ? detallesNuevos[original.ProductoServicioId].Cantidad
                            : 0;
                        if (original.Cantidad > nuevaCantidad)
                        {
                            int cantidadADevolver = original.Cantidad - nuevaCantidad;
                            await _stockService.DevolverStockAsync(original.ProductoServicioId, cantidadADevolver);
                        }
                    }
                }

                // Restar stock de productos nuevos o aumentados y validar
                foreach (var nuevo in dto.Detalles)
                {
                    var producto = await _context.ProductosServicios.FindAsync(
                        nuevo.ProductoServicioId
                    );
                    if (producto == null)
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(
                            new
                            {
                                status = 400,
                                error = "Bad Request",
                                message = $"ProductoServicioId {nuevo.ProductoServicioId} no existe.",
                            }
                        );
                    }

                    if (producto.EsAlmacenable == true)
                    {
                        int cantidadOriginal = detallesOriginales.ContainsKey(
                            nuevo.ProductoServicioId
                        )
                            ? detallesOriginales[nuevo.ProductoServicioId].Cantidad
                            : 0;
                        if (nuevo.Cantidad > cantidadOriginal)
                        {
                            int cantidadARestar = nuevo.Cantidad - cantidadOriginal;
                            var exito = await _stockService.UpdateStockAsync(nuevo.ProductoServicioId, cantidadARestar);
                            if (!exito)
                            {
                                await transaction.RollbackAsync();
                                return new BadRequestObjectResult(
                                    new
                                    {
                                        status = 400,
                                        error = "Bad Request",
                                        message = $"No hay stock suficiente para el producto {producto.Nombre}. Stock disponible: {producto.Cantidad}.",
                                    }
                                );
                            }
                        }
                    }
                }

                // Actualizar detalles
                _context.DetalleAtencion.RemoveRange(atencion.DetalleAtencion);
                foreach (var detalleAgrupado in detallesNuevos.Values)
                {
                    var nuevoDetalle = new DetalleAtencion
                    {
                        AtencionId = id,
                        ProductoServicioId = detalleAgrupado.ProductoServicioId,
                        Cantidad = detalleAgrupado.Cantidad,
                        PrecioUnitario = detalleAgrupado.PrecioUnitario,
                        Observacion = detalleAgrupado.Observacion,
                    };
                    _context.DetalleAtencion.Add(nuevoDetalle);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(
                    new { status = 200, message = "Detalles de venta actualizados correctamente." }
                );
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error al actualizar detalles de venta");
                return StatusCode(
                    500,
                    new
                    {
                        status = 500,
                        error = "Internal Server Error",
                        message = "Ocurrió un error al actualizar los detalles de la venta.",
                        details = ex.Message,
                    }
                );
            }
        }

        // POST: api/detalleatencion
        [HttpPost]
        public async Task<IActionResult> PostDetalleAtencion(DetalleAtencion detalleAtencion)
        {
            if (!await _context.Atencion.AnyAsync(a => a.Id == detalleAtencion.AtencionId))
            {
                return BadRequest(
                    new
                    {
                        status = 400,
                        error = "Bad Request",
                        message = "El ID de atención no es válido.",
                    }
                );
            }

            if (
                !await _context.ProductosServicios.AnyAsync(p =>
                    p.Id == detalleAtencion.ProductoServicioId
                )
            )
            {
                return BadRequest(
                    new
                    {
                        status = 400,
                        error = "Bad Request",
                        message = "El ID del producto o servicio no es válido.",
                    }
                );
            }

            _context.DetalleAtencion.Add(detalleAtencion);
            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetDetalleAtencion),
                new { id = detalleAtencion.Id },
                new
                {
                    status = 201,
                    message = "Detalle de atención registrado correctamente.",
                    detalleAtencion,
                }
            );
        }

        // PUT: api/detalleatencion/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> PutDetalleAtencion(int id, DetalleAtencion detalleAtencion)
        {
            if (id != detalleAtencion.Id)
            {
                return BadRequest(
                    new
                    {
                        status = 400,
                        error = "Bad Request",
                        message = "El ID en la URL no coincide con el ID del cuerpo de la solicitud.",
                    }
                );
            }

            _context.Entry(detalleAtencion).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.DetalleAtencion.Any(e => e.Id == id))
                {
                    return NotFound(
                        new
                        {
                            status = 404,
                            error = "Not Found",
                            message = "El detalle de atención no existe.",
                        }
                    );
                }

                throw;
            }

            return Ok(
                new
                {
                    status = 200,
                    message = "Detalle de atención actualizado correctamente.",
                    detalleAtencion,
                }
            );
        }

        // DELETE: api/detalleatencion/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDetalleAtencion(int id)
        {

            var detalleAtencion = await _context.DetalleAtencion
                .Include(d => d.ProductoServicio)
                .FirstOrDefaultAsync(d => d.Id == id);
            if (detalleAtencion == null)
            {
                return NotFound(
                    new
                    {
                        status = 404,
                        error = "Not Found",
                        message = "El detalle de atención no existe.",
                    }
                );
            }

            // Devolver stock si corresponde
            if (detalleAtencion.ProductoServicio != null && detalleAtencion.ProductoServicio.EsAlmacenable)
            {
                await _stockService.DevolverStockAsync(detalleAtencion.ProductoServicioId, detalleAtencion.Cantidad);
            }

            var atencionExiste = await _context.Atencion.AnyAsync(a =>
                a.Id == detalleAtencion.AtencionId
            );
            if (!atencionExiste)
            {
                return BadRequest(
                    new
                    {
                        status = 400,
                        error = "Bad Request",
                        message = "No se puede eliminar el detalle de atención porque la atención asociada ya no existe.",
                        details = new
                        {
                            DetalleAtencionId = id,
                            Relacion = "Atencion",
                            Motivo = "Clave foránea inválida",
                        },
                    }
                );
            }

            var productoServicioExiste = await _context.ProductosServicios.AnyAsync(p =>
                p.Id == detalleAtencion.ProductoServicioId
            );
            if (!productoServicioExiste)
            {
                return BadRequest(
                    new
                    {
                        status = 400,
                        error = "Bad Request",
                        message = "No se puede eliminar el detalle de atención porque el producto o servicio asociado ya no existe.",
                        details = new
                        {
                            DetalleAtencionId = id,
                            Relacion = "ProductoServicio",
                            Motivo = "Clave foránea inválida",
                        },
                    }
                );
            }

            _context.DetalleAtencion.Remove(detalleAtencion);
            await _context.SaveChangesAsync();

            return Ok(
                new
                {
                    status = 200,
                    message = "Detalle de atención eliminado correctamente.",
                    detalleAtencionEliminadoId = id,
                }
            );
        }
    }
}
