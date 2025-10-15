using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using backend.Models;

namespace backend.Services.Interfaces
{
    public interface ICierreDiarioService
    {
        Task<CierreDiario> CrearCierreAsync(CierreDiario cierre);
        Task<List<CierreDiario>> ObtenerCierresAsync();
        Task<CierreDiario?> ObtenerCierrePorIdAsync(int id);
        Task<bool> BloquearCierreAsync(int id);

        Task<CierreDiario?> ObtenerCierrePorFechaAsync(DateTime fecha);
        Task<object> ObtenerResumenDelDiaAsync(DateTime fecha);
        Task<bool> ValidarPasswordAsync(int userId, string password);
        Task<(bool existe, bool estaCerrada)> VerificarVentaCerradaAsync(int atencionId);
    }
}
