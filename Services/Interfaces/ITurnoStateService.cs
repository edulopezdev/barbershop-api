using System.Threading.Tasks;

namespace backend.Services.Interfaces
{
    public interface ITurnoStateService
    {
        /// <summary>
        /// Actualiza estados de turnos según reglas de negocio:
        /// - Si FechaHora &lt; ahora y Estado = Pendiente(1) => Caducado(4)
        /// - Si FechaHora &lt; ahora y Estado = Confirmado(2) => Atendido(5)
        /// Devuelve la cantidad de turnos actualizados (caducados, atendidos).
        /// </summary>
        Task<(int caducados, int atendidos)> UpdateExpiredTurnosAsync();
    }
}
