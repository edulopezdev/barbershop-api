using System;
using System.Security.Claims;
using System.Threading.Tasks;
using backend.Dtos;
using backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/dashboard")]
    public class DashboardController : ControllerBase
    {
        private readonly IClienteDashboardService _clienteService;
        private readonly IBarberoDashboardService _barberoService;

        public DashboardController(
            IClienteDashboardService clienteService,
            IBarberoDashboardService barberoService
        )
        {
            _clienteService = clienteService;
            _barberoService = barberoService;
        }

        private int? GetUserIdFromClaims()
        {
            var claim =
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value
                ?? User.FindFirst("userId")?.Value;
            if (int.TryParse(claim, out var id))
                return id;
            return null;
        }

        // GET: api/dashboard/cliente
        [HttpGet("cliente")]
        [Authorize]
        public async Task<IActionResult> GetClienteDashboard()
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized(new { mensaje = "Token inválido" });

            var dto = await _clienteService.GetClienteDashboardAsync(userId.Value);
            return Ok(
                new
                {
                    status = 200,
                    message = "Dashboard cliente obtenido correctamente.",
                    data = dto,
                }
            );
        }

        // GET: api/dashboard/barbero
        [HttpGet("barbero")]
        [Authorize(Roles = "Barbero,Administrador")]
        public async Task<IActionResult> GetBarberoDashboard()
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized(new { mensaje = "Token inválido" });

            var dto = await _barberoService.GetBarberoDashboardAsync(userId.Value);
            return Ok(
                new
                {
                    status = 200,
                    message = "Dashboard barbero obtenido correctamente.",
                    data = dto,
                }
            );
        }

        // GET: api/dashboard/barbero/cortes-mes
        [HttpGet("barbero/cortes-mes")]
        [Authorize(Roles = "Barbero,Administrador")]
        public async Task<IActionResult> GetCortesMes(
            [FromQuery] int? year = null,
            [FromQuery] int? month = null
        )
        {
            var uid = GetUserIdFromClaims();
            if (uid == null)
                return Unauthorized(new { mensaje = "Token inválido" });

            var cortes = await _barberoService.GetCortesMesAsync(uid.Value, year, month);
            return Ok(
                new
                {
                    status = 200,
                    message = "Cortes del mes obtenidos.",
                    data = cortes,
                }
            );
        }

        // (Opcional) añadir otros endpoints: admin, métricas globales
    }
}
