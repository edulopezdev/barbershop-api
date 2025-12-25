using System;
using System.Collections.Generic;

namespace backend.Dtos
{
    /// <summary>
    /// DTO para la respuesta del dashboard del cliente.
    /// Contiene información consolidada que la app cliente necesita en su pantalla principal.
    /// </summary>
    public class ClienteDashboardDto
    {
        public ProximoTurnoDto? ProximoTurno { get; set; }
        public List<ServicioResumenDto> Servicios { get; set; } = new();
        public BarberiaInfoDto Barberia { get; set; } = new BarberiaInfoDto();
        public ResumenClienteDto? ResumenCliente { get; set; }
        public UltimoServicioDto? UltimoServicio { get; set; }
    }

    public class BarberiaInfoDto
    {
        public string? Nombre { get; set; }
        public string? Direccion { get; set; }
        public string? MapsUrl { get; set; }
        public string? Whatsapp { get; set; }
        public string? Email { get; set; }
        public string? Instagram { get; set; }

        // Horarios (strings tal como están en configuración)
        public string? HorarioMatutinoInicio { get; set; }
        public string? HorarioMatutinoFin { get; set; }
        public string? HorarioVespertinoInicio { get; set; }
        public string? HorarioVespertinoFin { get; set; }
        // NOTE: No exponemos configuraciones internas (últimos turnos, políticas, flags) al cliente.
    }

    public class UltimoServicioDto
    {
        public int DetalleAtencionId { get; set; }
        public int AtencionId { get; set; }
        public int ProductoServicioId { get; set; }
        public string? Nombre { get; set; }
        public string? Descripcion { get; set; }
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
    }

    /// <summary>
    /// Próximo turno del cliente.
    /// </summary>
    public class ProximoTurnoDto
    {
        public int Id { get; set; }
        public DateTime FechaHora { get; set; }
        public int EstadoId { get; set; }
        public int BarberoId { get; set; }
        public int ClienteId { get; set; }
        public string? BarberoNombre { get; set; }
        public string? EstadoNombre { get; set; }
    }

    /// <summary>
    /// Resumen de un servicio (nombre, precio).
    /// </summary>
    public class ServicioResumenDto
    {
        public int Id { get; set; }
        public string? Nombre { get; set; }
        public decimal? Precio { get; set; }
    }

    /// <summary>
    /// Resumen rápido de actividad del cliente.
    /// </summary>
    public class ResumenClienteDto
    {
        public int AtencionesCount { get; set; }
        public UltimaAtencionDto? UltimaAtencion { get; set; }
    }

    /// <summary>
    /// Última atención del cliente.
    /// </summary>
    public class UltimaAtencionDto
    {
        public int Id { get; set; }
        public DateTime Fecha { get; set; }
    }
}
