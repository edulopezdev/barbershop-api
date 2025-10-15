using System.Security.Claims;
using backend.Dtos;
using backend.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AtencionController : ControllerBase
    {
        private readonly IAtencionService _atencionService;
        private readonly IVentaService _ventaService;
        private readonly ILogger<AtencionController> _logger;

        public AtencionController(
            IAtencionService atencionService,
            IVentaService ventaService,
            ILogger<AtencionController> logger
        )
        {
            _atencionService = atencionService;
            _ventaService = ventaService;
            _logger = logger;
        }

        // GET: api/atencion
        [HttpGet]
        public async Task<IActionResult> GetAtenciones(int page = 1, int pageSize = 10)
        {
            _logger.LogInformation(
                "GetAtenciones called with page={Page}, pageSize={PageSize}",
                page,
                pageSize
            );
            var result = await _atencionService.ObtenerAtenciones(page, pageSize);
            return result;
        }

        // GET: api/atencion/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetAtencion(int id)
        {
            _logger.LogInformation("GetAtencion called with id={Id}", id);
            var result = await _atencionService.ObtenerAtencionPorId(id);
            return result;
        }

        // POST: api/atencion
        [HttpPost]
        public async Task<IActionResult> PostAtencion([FromBody] CrearAtencionDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var barberoId = await _ventaService.GetBarberoId(User);

            if (barberoId == null)
            {
                return Unauthorized(
                    new
                    {
                        status = 401,
                        error = "Unauthorized",
                        message = "Usuario no autenticado.",
                    }
                );
            }

            var result = await _atencionService.CrearAtencionAsync(dto, barberoId.Value);
            return result;
        }

        // PUT: api/atencion/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> PutAtencion(int id, [FromBody] ActualizarAtencionDto dto)
        {
            var result = await _atencionService.ActualizarAtencion(id, dto);
            return result;
        }

        // DELETE: api/atencion/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAtencion(int id)
        {
            var result = await _atencionService.EliminarAtencion(id);
            return result;
        }

        // GET: api/atencion/resumen-barbero
        [HttpGet("resumen-barbero")]
        public async Task<IActionResult> GetResumenBarbero(int barberoId, int mes = 0, int anio = 0)
        {
            var result = await _atencionService.ObtenerResumenBarbero(barberoId, mes, anio);
            return result;
        }
    }
}
