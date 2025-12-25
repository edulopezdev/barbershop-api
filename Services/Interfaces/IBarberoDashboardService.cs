using System.Threading.Tasks;
using backend.Dtos;

namespace backend.Services.Interfaces
{
    public interface IBarberoDashboardService
    {
        Task<BarberoDashboardDto> GetBarberoDashboardAsync(int barberoId);
        Task<BarberoCortesMesDto> GetCortesMesAsync(
            int barberoId,
            int? year = null,
            int? month = null
        );
    }
}
