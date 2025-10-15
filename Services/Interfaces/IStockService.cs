using System.Threading.Tasks;

namespace backend.Services.Interfaces
{
    public interface IStockService
    {
        Task<bool> UpdateStockAsync(int productoServicioId, int cantidad);
        Task DevolverStockAsync(int productoServicioId, int cantidad);
        Task<int> GetStockAsync(int productoServicioId);
        Task SendLowStockAlertAsync(int productoServicioId);
        /// <summary>
        /// Envía un solo mail con la lista de productos almacenables con bajo stock (solo si hay alguno).
        /// </summary>
        Task SendLowStockSummaryEmailAsync();
    }
}
