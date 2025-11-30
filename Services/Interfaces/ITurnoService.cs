using System.Collections.Generic;
using backend.Models;

namespace backend.Services
{
    public interface ITurnoService
    {
        Task<IEnumerable<Turno>> GetAllTurnosAsync();
        Task<Turno?> GetTurnoByIdAsync(int id);
        Task<Turno> CreateTurnoAsync(Turno turno);
        Task<Turno?> UpdateTurnoAsync(int id, Turno updatedTurno);
        Task<bool> DeleteTurnoAsync(int id);
        Task<bool> BarberoExisteAsync(int barberoId);
        Task<IEnumerable<Horario>> GetDisponibilidadAsync(
            int barberoId,
            DateTime fechaInicio,
            DateTime fechaFin
        );
        Task<IEnumerable<object>> GetTurnosOcupadosAsync(
            int barberoId,
            DateTime fechaInicio,
            DateTime fechaFin
        );
    }
}

namespace backend.Models
{
    public class ApiResponse<T>
    {
        public required bool Success { get; set; }
        public required T Data { get; set; }
        public required string Message { get; set; }
        public object? Errors { get; set; }
    }
}
