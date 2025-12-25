using System.Threading.Tasks;
using backend.Dtos;

namespace backend.Services.Interfaces
{
    /// <summary>
    /// Servicio para obtener información consolidada del dashboard del cliente.
    /// </summary>
    public interface IClienteDashboardService
    {
        /// <summary>
        /// Obtiene el dashboard completo del cliente autenticado.
        /// </summary>
        /// <param name="usuarioId">ID del cliente autenticado.</param>
        /// <returns>DTO con la información consolidada del dashboard.</returns>
        Task<ClienteDashboardDto> GetClienteDashboardAsync(int usuarioId);
    }
}
