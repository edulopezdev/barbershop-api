using System;
using System.Threading.Tasks;

namespace backend.Services.Interfaces
{
    public interface IVentaService
    {
        Task<object> ObtenerVentasAsync(
            int page,
            int pageSize,
            string? clienteNombre,
            int? productoServicioId,
            string? productoNombre,
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            string ordenarPor,
            bool ordenDescendente,
            decimal? montoMin,
            decimal? montoMax,
            string? estadoPago // nuevo parámetro
        );
        Task<int?> GetBarberoId(System.Security.Claims.ClaimsPrincipal user);
    }
}
