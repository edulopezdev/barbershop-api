using System.Security.Claims;
using backend.Dtos;
using backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers
{
    [ApiController]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("api/legacy/dashboard/barbero")]
    [Obsolete("Usar /api/dashboard/barbero en DashboardController (unificado)")]
    public class BarberosDashboardController : ControllerBase
    {
        private readonly IBarberoDashboardService _service;

        public BarberosDashboardController(IBarberoDashboardService service)
        {
            _service = service;
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

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetDashboard()
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("Token inválido");

            var dto = await _service.GetBarberoDashboardAsync(userId.Value);
            return Ok(
                new
                {
                    status = 200,
                    message = "Dashboard del barbero obtenido correctamente.",
                    data = dto,
                }
            );
        }
    }
}
