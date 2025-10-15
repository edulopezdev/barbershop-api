using System.Threading.Tasks;
using backend.Dtos;
using backend.Models;
using Microsoft.AspNetCore.Mvc;

namespace backend.Services.Interfaces
{
    public interface IAtencionService
    {
        Task<IActionResult> ObtenerAtenciones(int page, int pageSize);
        Task<IActionResult> ObtenerAtencionPorId(int id);
        Task<IActionResult> CrearAtencionAsync(CrearAtencionDto dto, int barberoId);
        Task<IActionResult> ActualizarAtencion(int id, ActualizarAtencionDto dto);
        Task<IActionResult> EliminarAtencion(int id);
        Task<IActionResult> ObtenerResumenBarbero(int barberoId, int mes, int anio);
    }
}
