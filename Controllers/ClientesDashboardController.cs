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
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("api/legacy/dashboard/cliente")]
    [Obsolete("Usar /api/dashboard/cliente en DashboardController (unificado)")]
    [Authorize]
    public class ClientesDashboardController : ControllerBase
    {
        private readonly IClienteDashboardService _dashboardService;

        public ClientesDashboardController(IClienteDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        // GET: /api/dashboard/cliente
        // Devuelve un objeto con la información que la app cliente necesita para su pantalla principal.
        [HttpGet]
        public async Task<IActionResult> GetDashboard()
        {
            // Identificar usuario desde claims de forma segura
            int? usuarioId = null;
            if (User.Identity?.IsAuthenticated == true)
            {
                var idClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");

                if (idClaim != null && int.TryParse(idClaim.Value, out var id))
                {
                    usuarioId = id;
                }
            }

            // Si no hay usuario autenticado, devolver error
            if (!usuarioId.HasValue)
            {
                return Unauthorized(new { mensaje = "Usuario no autenticado." });
            }

            // Delegar al servicio para obtener el dashboard completo
            var dashboard = await _dashboardService.GetClienteDashboardAsync(usuarioId.Value);
            return Ok(dashboard);
        }
    }
}
